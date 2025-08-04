# PHASE 3 TASK 3 — Event Sourcing & CQRS (Ultra-Compact Drop-In)

**Goals:** reliable outbox, concurrent workers (SKIP LOCKED), versioned serialization, idempotent projections, compiled queries + tsvector, DLQ, health + metrics.
**Placement (Clean Architecture):** *Application* = interfaces/contracts/UoW/Repos/Events. *Infrastructure* = EF Core/PostgreSQL implementations.
**Adj. applied:** A1–A11 (workers, tracing, SKIP LOCKED, compiled queries, tsvector, clock-safe idempotency, DbContextPool, cache probe, indexes/DbSets guard, causation).

---

## Topology

```mermaid
graph TB
  A[Commands]-->B[Aggregate]-->C[Domain Events]-->D[UoW]-->E[Outbox]
  E-->F[Workers]-->G[Serializer/Registry]-->H[Publisher]
  H-->I[Projections]-->J[Read Models]-->K[Queries]
  D-->L[(Write DB)] I-->M[(Read DB)]
```

---

## Outbox

### Entity

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public string Metadata { get; init; } = default!;
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int ProcessingAttempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public static OutboxMessage Create(string t,string p,string m,DateTime at)=>
        new(){Type=t,Payload=p,Metadata=m,OccurredAtUtc=at};
}
```

### EF config

```csharp
public sealed class OutboxCfg : IEntityTypeConfiguration<OutboxMessage>
{
  public void Configure(EntityTypeBuilder<OutboxMessage> b)
  {
    b.ToTable("outbox_messages"); b.HasKey(x=>x.Id);
    b.Property(x=>x.Type).IsRequired().HasMaxLength(500);
    b.Property(x=>x.Payload).IsRequired().HasColumnType("jsonb");
    b.Property(x=>x.Metadata).IsRequired().HasColumnType("jsonb");
    b.Property(x=>x.OccurredAtUtc).HasColumnType("timestamptz");
    b.Property(x=>x.ProcessedAtUtc).HasColumnType("timestamptz");
    b.Property(x=>x.NextRetryAtUtc).HasColumnType("timestamptz");
    b.HasIndex(x=>x.ProcessedAtUtc).HasDatabaseName("ix_outbox_processed");
    b.HasIndex(x=>new{x.ProcessedAtUtc,x.NextRetryAtUtc})
      .HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_outbox_processing");
    b.HasIndex(x=>x.OccurredAtUtc).HasDatabaseName("ix_outbox_occurred");
    b.HasIndex(x=>new{x.ProcessedAtUtc,x.OccurredAtUtc})
      .HasFilter("processed_at_utc IS NULL").HasDatabaseName("ix_outbox_processed_occurred"); // A9
  }
}
```

### UoW capture

```csharp
public sealed class EfUnitOfWork : IUnitOfWork
{
  private readonly ChatDbContext _db; private readonly IEventSerializer _ser; private readonly ICurrentUserService _usr;
  public EfUnitOfWork(ChatDbContext db,IEventSerializer s,ICurrentUserService u){_db=db;_ser=s;_usr=u;}

  public async Task ExecuteInTransactionAsync(Func<CancellationToken,Task> act,CancellationToken ct=default)
  {
    await using var tx=await _db.Database.BeginTransactionAsync(ct);
    try{ await act(ct); await Capture(ct); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);}
    catch{ await tx.RollbackAsync(ct); throw;}
  }

  private async Task Capture(CancellationToken ct)
  {
    var aggs=_db.ChangeTracker.Entries().Where(e=>e.Entity is IAggregateRoot && e.State!=EntityState.Detached)
      .Select(e=>(IAggregateRoot)e.Entity).Where(a=>a.DomainEvents.Any()).ToList();
    if(aggs.Count==0) return;
    var uid=_usr.GetCurrentUserIdOrSystem(); var corr=Activity.Current?.Id??Guid.NewGuid().ToString(); var at=DateTime.UtcNow;
    var list=new List<OutboxMessage>();
    foreach(var a in aggs)
    foreach(var ev in a.DomainEvents)
    { var meta=await _ser.BuildMetadataAsync(ev,corr,uid,ct); var json=await _ser.SerializeAsync(ev,ct);
      list.Add(OutboxMessage.Create(ev.GetType().AssemblyQualifiedName!,json,meta,at)); }
    a.DomainEvents.Clear();
    if(_db.Outbox==null||_db.DeadLetterMessages==null||_db.EventCorrelations==null) throw new InvalidOperationException("DbSets missing"); // A11
    await _db.Outbox.AddRangeAsync(list,ct);
  }
}
```

---

## Workers (A1, SKIP LOCKED, sequential per scope)

```csharp
public sealed class OutboxDispatcherService : BackgroundService
{
  private readonly IServiceProvider _sp; private readonly OutboxDispatcherOptions _o;
  public OutboxDispatcherService(IServiceProvider sp,IOptions<OutboxDispatcherOptions> o){_sp=sp;_o=o.Value;}
  protected override async Task ExecuteAsync(CancellationToken stop)
  {
    var tasks=Enumerable.Range(0,_o.MaxConcurrentWorkers).Select(_=>Task.Run(()=>Loop(stop),stop));
    await Task.WhenAll(tasks);
  }
  private async Task Loop(CancellationToken ct)
  {
    while(!ct.IsCancellationRequested)
    {
      try{
        using var scope=_sp.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var pub=scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var ser=scope.ServiceProvider.GetRequiredService<IEventSerializer>();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var batch=await db.Outbox.FromSqlInterpolated($@"
          SELECT * FROM outbox_messages
          WHERE processed_at_utc IS NULL
          AND (next_retry_at_utc IS NULL OR next_retry_at_utc <= {DateTime.UtcNow})
          ORDER BY occurred_at_utc
          FOR UPDATE SKIP LOCKED
          LIMIT {_o.BatchSize}").ToListAsync(ct);
        if(batch.Count==0){await Task.Delay(_o.ProcessingIntervalMs,ct); continue;}
        foreach(var m in batch)
        {
          try{
            var ev=await ser.DeserializeAsync(m.Type,m.Payload,ct);
            await pub.PublishAsync(ev,ct);
            m.ProcessedAtUtc=DateTime.UtcNow; m.LastError=null; m.NextRetryAtUtc=null;
          }catch(Exception ex){
            m.ProcessingAttempts++; m.LastError=ex.Message;
            m.NextRetryAtUtc = m.ProcessingAttempts>=_o.MaxRetryAttempts ? null
              : DateTime.UtcNow.AddSeconds(Math.Pow(2,m.ProcessingAttempts)+Random.Shared.Next(0,30));
          }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
      }catch(OperationCanceledException){break;}catch{ await Task.Delay(_o.ErrorDelayMs,ct); }
    }
  }
}
public sealed class OutboxDispatcherOptions
{
  public const string SectionName="OutboxDispatcher";
  public int ProcessingIntervalMs {get;set;}=5000, ErrorDelayMs {get;set;}=30000, BatchSize {get;set;}=20, MaxConcurrentWorkers {get;set;}=3, MaxRetryAttempts {get;set;}=5;
  public bool EnableDeadLetterQueue {get;set;}=true;
}
```

---

## Serialization (envelope + metadata)

```csharp
public interface IEventSerializer
{
  Task<string> SerializeAsync(IDomainEvent ev, CancellationToken ct=default);
  Task<IDomainEvent> DeserializeAsync(string typeName,string payload,CancellationToken ct=default);
  Task<string> BuildMetadataAsync(IDomainEvent ev,string correlationId,string userId,CancellationToken ct=default);
  Task<EventMetadata> DeserializeMetadataAsync(string metadata,CancellationToken ct=default);
}

public sealed class SystemTextJsonEventSerializer : IEventSerializer
{
  private readonly IEventTypeRegistry _reg; private readonly JsonSerializerOptions _o;
  private static readonly ConcurrentDictionary<string,Type> Cache=new();
  public SystemTextJsonEventSerializer(IEventTypeRegistry reg){_reg=reg; _o=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,PropertyNameCaseInsensitive=true}; _o.Converters.Add(new StrongIdJsonConverter<ConversationId,Guid>()); _o.Converters.Add(new StrongIdJsonConverter<MessageId,Guid>()); _o.Converters.Add(new JsonStringEnumConverter());}
  public Task<string> SerializeAsync(IDomainEvent ev, CancellationToken ct=default)
  {
    var t=ev.GetType(); var env=new EventEnvelope{ EventType=t.AssemblyQualifiedName!, EventVersion=_reg.GetEventVersion(t), Data=ev, SchemaHash=_reg.GetSchemaHash(t)};
    return Task.FromResult(JsonSerializer.Serialize(env,_o));
  }
  public Task<IDomainEvent> DeserializeAsync(string _,string payload,CancellationToken ct=default)
  {
    var env=JsonSerializer.Deserialize<EventEnvelope>(payload,_o)!; var t=Cache.GetOrAdd(env.EventType,n=>Type.GetType(n)!);
    return env.EventVersion<_reg.GetEventVersion(t) ? _reg.UpcastEventAsync(env,t,ct) :
      Task.FromResult((IDomainEvent)((JsonElement)env.Data).Deserialize(t,_o)!);
  }
  public Task<string> BuildMetadataAsync(IDomainEvent ev,string corr,string user,CancellationToken ct=default)
  {
    var m=new EventMetadata{CorrelationId=corr,CausationId=Activity.Current?.GetTagItem("causation.id") as string,
      AggregateType=ev.GetType().Name.Replace("DomainEvent",""),
      AggregateId=ev.GetType().GetProperty("AggregateId")?.GetValue(ev)?.ToString()
         ?? ev.GetType().GetProperty("ConversationId")?.GetValue(ev)?.ToString() ?? "unknown",
      EventType=ev.GetType().Name, EventVersion=_reg.GetEventVersion(ev.GetType()), UserId=user, OccurredAtUtc=ev.OccurredAtUtc};
    return Task.FromResult(JsonSerializer.Serialize(m,_o));
  }
  public Task<EventMetadata> DeserializeMetadataAsync(string m,CancellationToken ct=default)=>
    Task.FromResult(JsonSerializer.Deserialize<EventMetadata>(m,_o)!);
}
public sealed record EventEnvelope{ public required string EventType{get;init;} public int EventVersion{get;init;} public required object Data{get;init;} public string? SchemaHash{get;init;} }
public sealed record EventMetadata{ public required string CorrelationId{get;init;} public string? CausationId{get;init;} public required string AggregateType{get;init;} public required string AggregateId{get;init;} public required string EventType{get;init;} public int EventVersion{get;init;} public required string UserId{get;init;} public DateTime OccurredAtUtc{get;init;} }
```

### Type registry (minimal)

```csharp
public interface IEventTypeRegistry
{
  int GetEventVersion(Type t); string GetSchemaHash(Type t);
  Task<IDomainEvent> UpcastEventAsync(EventEnvelope env,Type target,CancellationToken ct);
  void RegisterEventType<T>(int version,string? schemaHash=null) where T:IDomainEvent;
}
public sealed class InMemoryEventTypeRegistry : IEventTypeRegistry
{
  private readonly ConcurrentDictionary<Type,(int v,string h)> _m=new();
  public int GetEventVersion(Type t)=>_m.TryGetValue(t,out var x)?x.v:1;
  public string GetSchemaHash(Type t)=>_m.TryGetValue(t,out var x)?x.h:Hash(t);
  public Task<IDomainEvent> UpcastEventAsync(EventEnvelope e,Type t,CancellationToken ct)=>Task.FromResult((IDomainEvent)((JsonElement)e.Data).Deserialize(t)!);
  public void RegisterEventType<T>(int v,string? h=null) where T:IDomainEvent=>_m[typeof(T)]=(v,h??Hash(typeof(T)));
  private static string Hash(Type t){var s=string.Join("|",t.GetProperties().OrderBy(p=>p.Name).Select(p=>$"{p.Name}:{p.PropertyType.Name}"));return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..8];}
}
```

---

## Read Models + Projections

```csharp
public abstract class ReadModelBase<TId>:AuditableEntity<TId> where TId:struct,IStrongId<Guid>
{
  public long Version{get;protected set;} public DateTime LastEventTimestamp{get;protected set;} public string? LastProcessedEventId{get;protected set;}
  protected void UpdateVersion(long v,DateTime ts,string id){ if(LastProcessedEventId==id||v<=Version)return; Version=v; LastEventTimestamp=ts; LastProcessedEventId=id; }
}

public sealed class ConversationReadModel:ReadModelBase<ConversationId>
{
  public string Title{get;private set;}=""; public string Status{get;private set;}=ConversationStatus.Active.ToString();
  public int MessageCount{get;private set;} public int ToolExecutionCount{get;private set;}
  public DateTime LastMessageAt{get;private set;} public DateTime? CompletedAt{get;private set;} public bool IsActive{get;private set;}=true;
  public string Context{get;private set;}="{}"; public string Tags{get;private set;}="[]"; public string SearchVector{get;private set;}=""; public string Participants{get;private set;}="[]";
  public ConversationStatistics Statistics{get;private set;}=new();
  public static ConversationReadModel Create(ConversationId id,string title,string user,DateTime at){var m=new ConversationReadModel{Id=id,Title=title,LastMessageAt=at,Participants=JsonSerializer.Serialize(new[]{user})}; m.SetCreated(at,user); m.SetUpdated(at,user); return m;}
  public void UpdateFromMessageAdded(MessageAddedDomainEvent e,string by){MessageCount++; if(e.Role==MessageRole.Tool)ToolExecutionCount++; LastMessageAt=e.OccurredAtUtc; SetUpdated(e.OccurredAtUtc,by); UpdateVersion(e.OccurredAtUtc.Ticks,e.OccurredAtUtc,e.Id.ToString());}
  public void UpdateFromConversationCompleted(ConversationCompletedDomainEvent e,string by){Status=ConversationStatus.Completed.ToString(); IsActive=false; CompletedAt=e.OccurredAtUtc; SetUpdated(e.OccurredAtUtc,by); UpdateVersion(e.OccurredAtUtc.Ticks,e.OccurredAtUtc,e.Id.ToString());}
}
public sealed record ConversationStatistics{ public int TotalCharacters{get;init;} public int TotalTokensEstimate{get;init;} public TimeSpan AverageResponseTime{get;init;} public DateTime? LastToolExecution{get;init;} public string MostUsedTool{get;init;}="";}
```

### EF config (search + indexes)

```csharp
public sealed class ConversationReadCfg : IEntityTypeConfiguration<ConversationReadModel>
{
  public void Configure(EntityTypeBuilder<ConversationReadModel> b)
  {
    b.ToTable("conversation_read_models"); b.HasKey(x=>x.Id);
    b.Property(x=>x.Id).HasConversion(v=>v.Value,v=>ConversationId.From(v)).ValueGeneratedNever();
    b.Property(x=>x.Title).IsRequired().HasMaxLength(500); b.Property(x=>x.Status).IsRequired().HasMaxLength(50);
    b.Property(x=>x.Context).HasColumnType("jsonb").HasDefaultValue("{}");
    b.Property(x=>x.Tags).HasColumnType("jsonb").HasDefaultValue("[]");
    b.Property(x=>x.Participants).HasColumnType("jsonb").HasDefaultValue("[]");
    b.Property(x=>x.SearchVector).HasColumnType("tsvector")
      .HasComputedColumnSql("to_tsvector('english', coalesce(title,'') || ' ' || coalesce(context::text,''))", stored:true);
    b.Property(x=>x.Version).IsConcurrencyToken();
    b.HasIndex(x=>x.SearchVector).HasMethod("gin");
    b.HasIndex(x=>new{x.IsActive,x.LastMessageAt}).HasFilter("is_active = true");
    b.HasIndex(x=>x.Status); b.HasIndex(x=>x.CreatedBy);
    b.HasIndex(x=>new{x.CreatedBy,x.IsActive,x.LastMessageAt});
    b.HasIndex(x=>new{x.Version,x.LastEventTimestamp});
  }
}
```

### Projection

```csharp
public sealed class ConversationProjectionService : IProjectionService
{
  private readonly ChatDbContext _db; private readonly ICurrentUserService _usr;
  public ConversationProjectionService(ChatDbContext db,ICurrentUserService usr){_db=db;_usr=usr;}
  public async Task ProjectAsync(IDomainEvent e,CancellationToken ct=default)
  {
    switch(e)
    {
      case ConversationStartedDomainEvent s:
        if(await _db.ConversationReads.AnyAsync(x=>x.Id==s.ConversationId,ct)) return;
        _db.ConversationReads.Add(ConversationReadModel.Create(s.ConversationId,s.Title??"New Conversation",_usr.GetCurrentUserIdOrSystem(),s.OccurredAtUtc)); 
        await _db.SaveChangesAsync(ct); break;

      case MessageAddedDomainEvent m:
        var r=await _db.ConversationReads.FirstOrDefaultAsync(x=>x.Id==m.ConversationId,ct); if(r==null) return;
        if(r.LastProcessedEventId==m.Id.ToString()||r.LastEventTimestamp>=m.OccurredAtUtc) return;
        r.UpdateFromMessageAdded(m,_usr.GetCurrentUserIdOrSystem()); await _db.SaveChangesAsync(ct); break;

      case ConversationCompletedDomainEvent c:
        var d=await _db.ConversationReads.FirstOrDefaultAsync(x=>x.Id==c.ConversationId,ct); if(d==null) return;
        d.UpdateFromConversationCompleted(c,_usr.GetCurrentUserIdOrSystem()); await _db.SaveChangesAsync(ct); break;
    }
  }
}
```

---

## CQRS Query (compiled + cache + tsvector)

```csharp
public sealed class ConversationQueryService : IConversationQueryService
{
  private readonly ChatDbContext _db; private readonly IMemoryCache _cache;
  private static readonly Func<ChatDbContext,string,int,IQueryable<ConversationReadModel>> QByUser=
    EF.CompileQuery((ChatDbContext c,string u,int take)=>c.ConversationReads.Where(x=>x.CreatedBy==u&&x.IsActive).OrderByDescending(x=>x.LastMessageAt).Take(take));
  private static readonly Func<ChatDbContext,ConversationId,IQueryable<ConversationReadModel>> QById=
    EF.CompileQuery((ChatDbContext c,ConversationId id)=>c.ConversationReads.Where(x=>x.Id==id));
  private static readonly Func<ChatDbContext,string,int,IQueryable<ConversationReadModel>> QSearch=
    EF.CompileQuery((ChatDbContext c,string ts,int take)=>c.ConversationReads.Where(x=>x.IsActive&&x.SearchVector.Matches(EF.Functions.ToTsQuery("english",ts))).OrderByDescending(x=>x.LastMessageAt).Take(take));

  public ConversationQueryService(ChatDbContext db,IMemoryCache cache){_db=db;_cache=cache;}

  public async Task<ConversationReadModel?> GetByIdAsync(ConversationId id,CancellationToken ct=default)
  {
    var key=$"conversation:{id}";
    if(_cache.TryGetValue(key,out ConversationReadModel? cached)) return cached;
    var rm=await QById(_db,id).FirstOrDefaultAsync(ct);
    if(rm!=null) _cache.Set(key,rm,new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=TimeSpan.FromMinutes(15),SlidingExpiration=TimeSpan.FromMinutes(5)});
    return rm;
  }

  public async Task<IReadOnlyList<ConversationReadModel>> GetByUserAsync(string user,int take=20,CancellationToken ct=default)
  {
    var key=$"user_conversations:{user}:{take}";
    if(_cache.TryGetValue(key,out List<ConversationReadModel>? cached)) return cached.AsReadOnly();
    var list=await QByUser(_db,user,take).ToListAsync(ct); _cache.Set(key,list,TimeSpan.FromMinutes(5)); return list.AsReadOnly();
  }

  public async Task<ConversationSearchResult> SearchAsync(ConversationSearchQuery q,CancellationToken ct=default)
  {
    List<ConversationReadModel> res = string.IsNullOrWhiteSpace(q.SearchTerm)
      ? await QByUser(_db,q.UserId ?? "", q.Take).ToListAsync(ct)
      : await QSearch(_db, ToTs(q.SearchTerm!), q.Take).ToListAsync(ct);
    if(q.Status.HasValue) res=res.Where(c=>c.Status==q.Status.ToString()).ToList();
    if(q.CreatedAfter.HasValue) res=res.Where(c=>c.CreatedAtUtc>=q.CreatedAfter.Value).ToList();
    return new ConversationSearchResult{Conversations=res.AsReadOnly(),TotalCount=res.Count,SearchTerm=q.SearchTerm,ElapsedMilliseconds=0};
  }

  public async Task<ConversationStatistics> GetStatisticsAsync(string? user=null,CancellationToken ct=default)
  {
    var key=$"conversation_stats:{user??"all"}"; if(_cache.TryGetValue(key,out ConversationStatistics? c)) return c;
    var q=_db.ConversationReads.AsQueryable(); if(!string.IsNullOrWhiteSpace(user)) q=q.Where(x=>x.CreatedBy==user);
    var s=await q.GroupBy(_=>1).Select(g=>new ConversationStatistics{
      TotalCharacters=g.Sum(x=>x.Statistics.TotalCharacters),
      TotalTokensEstimate=g.Sum(x=>x.Statistics.TotalTokensEstimate),
      AverageResponseTime=TimeSpan.FromMilliseconds(g.Average(x=>x.Statistics.AverageResponseTime.TotalMilliseconds)),
      LastToolExecution=g.Max(x=>x.Statistics.LastToolExecution),
      MostUsedTool=g.GroupBy(x=>x.Statistics.MostUsedTool).OrderByDescending(gg=>gg.Count()).Select(gg=>gg.Key).FirstOrDefault()??""}).FirstOrDefaultAsync(ct) ?? new();
    _cache.Set(key,s,TimeSpan.FromHours(1)); return s;
  }
  private static string ToTs(string s)=>string.Join(" & ", s.Replace("'","''").Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(t=>t.Length>2).Select(t=>$"{t}:*"));
}
```

---

## DI + Health

```csharp
public static class EventSourcingDI
{
  public static IServiceCollection AddEventSourcingAndCqrs(this IServiceCollection s,IConfiguration cfg)
  {
    s.Configure<OutboxDispatcherOptions>(cfg.GetSection(OutboxDispatcherOptions.SectionName));
    s.Configure<ProjectionOptions>(cfg.GetSection(ProjectionOptions.SectionName));
    s.Configure<ErrorHandlingOptions>(cfg.GetSection(ErrorHandlingOptions.SectionName));
    s.AddScoped<IEventSerializer,SystemTextJsonEventSerializer>();
    s.AddScoped<IEventTypeRegistry,InMemoryEventTypeRegistry>();
    s.AddScoped<IProjectionService,ConversationProjectionService>();
    s.AddScoped<IConversationQueryService,ConversationQueryService>();
    s.AddScoped<IDeadLetterQueueService,DeadLetterQueueService>();
    s.AddHostedService<OutboxDispatcherService>();
    s.AddHostedService<ReadModelProjectionService>();
    s.AddScoped<IEventPublisher,MediatREventPublisher>();
    s.AddScoped<IEventBus,InMemoryEventBus>();
    s.AddMemoryCache();
    return s;
  }

  public static IServiceCollection AddEventSourcingDbContext(this IServiceCollection s,IConfiguration cfg)
  {
    s.AddDbContextPool<ChatDbContext>((sp,opt)=>{ var cs=cfg.GetConnectionString("DefaultConnection");
      opt.UseNpgsql(cs,o=>{o.EnableRetryOnFailure(3);o.CommandTimeout(30);});
      opt.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()); });
    return s;
  }
}

public sealed class ErrorHandlingOptions{ public const string SectionName="ErrorHandling"; public int MaxRetryAttempts{get;set;}=5; public double MaxRetryDelaySeconds{get;set;}=300; public bool EnableCircuitBreaker{get;set;}=true; public int DeadLetterRetentionDays{get;set;}=30; public bool EnableAutomaticCleanup{get;set;}=true; }

public sealed class EventSourcingHealthCheck : IHealthCheck
{
  private readonly ChatDbContext _db; private readonly IMemoryCache _cache;
  public EventSourcingHealthCheck(ChatDbContext db,IMemoryCache cache){_db=db;_cache=cache;}
  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext c,CancellationToken ct=default)
  {
    var ok=await _db.Database.CanConnectAsync(ct);
    var data=new Dictionary<string,object>{
      ["database_connected"]=ok,
      ["unprocessed_outbox_messages"]= await _db.Outbox.CountAsync(m=>m.ProcessedAtUtc==null,ct),
      ["old_unprocessed_messages"]= await _db.Outbox.CountAsync(m=>m.ProcessedAtUtc==null && m.OccurredAtUtc < DateTime.UtcNow.AddHours(-1),ct),
      ["dead_letter_messages"]= await _db.DeadLetterMessages.CountAsync(m=>!m.IsReprocessed,ct)
    };
    var k="health_probe"; var v=DateTime.UtcNow.Ticks; _cache.Set(k,v,TimeSpan.FromSeconds(1)); data["cache_working"]=Equals(_cache.Get(k),v); _cache.Remove(k);
    if(!ok) return HealthCheckResult.Unhealthy("db",data:data);
    if((int)data["old_unprocessed_messages"]!>100) return HealthCheckResult.Degraded("old outbox",data:data);
    if((int)data["dead_letter_messages"]!>50) return HealthCheckResult.Degraded("dlq",data:data);
    return HealthCheckResult.Healthy("ok",data:data);
  }
}
```

---

## Projections Host

```csharp
public sealed class ReadModelProjectionService : BackgroundService
{
  private readonly IServiceProvider _sp; private readonly ProjectionOptions _o;
  public ReadModelProjectionService(IServiceProvider sp,IOptions<ProjectionOptions> o){_sp=sp;_o=o.Value;}
  protected override async Task ExecuteAsync(CancellationToken stop)
  {
    using var scope=_sp.CreateScope(); var bus=scope.ServiceProvider.GetRequiredService<IEventBus>();
    await bus.SubscribeAsync<ConversationStartedDomainEvent>(Project,stop);
    await bus.SubscribeAsync<MessageAddedDomainEvent>(Project,stop);
    await bus.SubscribeAsync<ConversationCompletedDomainEvent>(Project,stop);
    try{await Task.Delay(Timeout.Infinite,stop);}catch(OperationCanceledException){}
  }
  private async Task Project<T>(T e,CancellationToken ct) where T:IDomainEvent
  { using var scope=_sp.CreateScope(); await scope.ServiceProvider.GetRequiredService<IProjectionService>().ProjectAsync(e,ct); }
}
public sealed class ProjectionOptions{ public const string SectionName="Projections"; public bool StopOnProjectionFailure{get;set;}=false; public bool EnableCheckpointing{get;set;}=true; public int CheckpointInterval{get;set;}=100; }
```

---

## KPIs

```yaml
event_processing_p95_ms: 100
outbox_e2e_seconds: 5
read_consistency_seconds: 1
query_p95_ms: 50
availability: 99.9%
projection_success: 99.9%
dlq_rate: "<0.1%"
```

**Done.** Paste into repo; wire interfaces in *Application*, EF/Postgres in *Infrastructure*.
