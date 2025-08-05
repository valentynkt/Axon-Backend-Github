using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Exception;

public class GrpcExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            // Re-throw gRPC exceptions as-is to preserve status codes
            throw;
        }
        catch (ArgumentException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (TimeoutException exception)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, exception.Message));
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (NotImplementedException exception)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, exception.Message));
        }
    }
}