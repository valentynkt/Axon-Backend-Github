DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
        CREATE SCHEMA identity;
    END IF;
END $EF$;


CREATE TABLE identity.axon_principals (
    id uuid NOT NULL,
    type character varying(50) NOT NULL,
    primary_email_hash character varying(64),
    preferred_language character varying(10) NOT NULL,
    risk_tier character varying(20) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    version bigint NOT NULL,
    CONSTRAINT pk_axon_principals PRIMARY KEY (id),
    CONSTRAINT ck_axon_principals_email_hash_format CHECK (primary_email_hash IS NULL OR (LENGTH(primary_email_hash) = 64 AND primary_email_hash ~ '^[a-f0-9]+$')),
    CONSTRAINT ck_axon_principals_preferred_language CHECK (preferred_language IN ('en', 'es', 'fr', 'de', 'ja', 'ko', 'zh')),
    CONSTRAINT ck_axon_principals_risk_tier CHECK (risk_tier IN ('low', 'medium', 'high', 'critical')),
    CONSTRAINT ck_axon_principals_type CHECK (type IN ('human', 'service'))
);


CREATE TABLE identity.wallets (
    id uuid NOT NULL,
    chain character varying(50) NOT NULL,
    address character varying(255) NOT NULL,
    first_seen_at timestamp with time zone NOT NULL,
    last_seen_at timestamp with time zone NOT NULL,
    provider character varying(100) NOT NULL,
    display_name character varying(255),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    version bigint NOT NULL,
    CONSTRAINT pk_wallets PRIMARY KEY (id)
);


CREATE TABLE identity.identity_credentials (
    id uuid NOT NULL,
    axon_id uuid NOT NULL,
    provider_type character varying(50) NOT NULL,
    issuer character varying(255) NOT NULL,
    subject character varying(255) NOT NULL,
    environment_id character varying(100),
    verified_at timestamp with time zone NOT NULL,
    last_seen_at timestamp with time zone NOT NULL,
    email_hash character varying(64),
    verification_method character varying(50) NOT NULL,
    session_public_key character varying(1000),
    device_id character varying(255),
    user_agent character varying(1000),
    ip_hash character varying(64),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    CONSTRAINT pk_identity_credentials PRIMARY KEY (id),
    CONSTRAINT fk_identity_credentials_axon_principals_axon_id FOREIGN KEY (axon_id) REFERENCES identity.axon_principals (id) ON DELETE CASCADE
);


CREATE TABLE identity.principal_chain_defaults (
    id uuid NOT NULL,
    axon_id uuid NOT NULL,
    chain_id character varying(50) NOT NULL,
    wallet_id uuid NOT NULL,
    axon_principal_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT pk_principal_chain_defaults PRIMARY KEY (id),
    CONSTRAINT fk_principal_chain_defaults_axon_principals_axon_principal_id FOREIGN KEY (axon_principal_id) REFERENCES identity.axon_principals (id) ON DELETE CASCADE
);


CREATE TABLE identity.wallet_ownerships (
    id uuid NOT NULL,
    axon_id uuid NOT NULL,
    wallet_id uuid NOT NULL,
    chain_id character varying(50) NOT NULL,
    proof_type character varying(50) NOT NULL,
    access_mode character varying(50) NOT NULL,
    state character varying(50) NOT NULL,
    first_linked_at timestamp with time zone NOT NULL,
    last_verified_at timestamp with time zone,
    label character varying(100),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL,
    deleted_at timestamp with time zone,
    CONSTRAINT pk_wallet_ownerships PRIMARY KEY (id),
    CONSTRAINT fk_wallet_ownerships_axon_principals_axon_id FOREIGN KEY (axon_id) REFERENCES identity.axon_principals (id) ON DELETE CASCADE
);


CREATE TABLE identity.wallet_tags (
    id uuid NOT NULL,
    wallet_id uuid NOT NULL,
    tag character varying(100) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone,
    CONSTRAINT pk_wallet_tags PRIMARY KEY (id),
    CONSTRAINT ck_wallet_tags_allowed_values CHECK (tag IN ('personal', 'business', 'trading', 'defi', 'gaming', 'nft', 'dao', 'test', 'main', 'hot', 'cold')),
    CONSTRAINT fk_wallet_tags_wallets_wallet_id FOREIGN KEY (wallet_id) REFERENCES identity.wallets (id) ON DELETE CASCADE
);


CREATE INDEX ix_axon_principals_created_at ON identity.axon_principals (created_at);


CREATE INDEX ix_axon_principals_is_deleted_type ON identity.axon_principals (is_deleted, type) WHERE is_deleted = false;


CREATE INDEX ix_axon_principals_primary_email_hash ON identity.axon_principals (primary_email_hash) WHERE primary_email_hash IS NOT NULL;


CREATE INDEX ix_axon_principals_type ON identity.axon_principals (type);


CREATE INDEX ix_credentials_axon_id ON identity.identity_credentials (axon_id);


CREATE INDEX ix_credentials_created_at ON identity.identity_credentials (created_at);


CREATE INDEX ix_credentials_last_seen_at ON identity.identity_credentials (last_seen_at);


CREATE UNIQUE INDEX ix_credentials_provider_issuer_subject_unique ON identity.identity_credentials (provider_type, issuer, subject);


CREATE UNIQUE INDEX ix_principal_chain_defaults_axon_chain_unique ON identity.principal_chain_defaults (axon_id, chain_id);


CREATE INDEX ix_principal_chain_defaults_axon_principal_id ON identity.principal_chain_defaults (axon_principal_id);


CREATE INDEX ix_principal_chain_defaults_chain_id ON identity.principal_chain_defaults (chain_id);


CREATE INDEX ix_principal_chain_defaults_wallet_id ON identity.principal_chain_defaults (wallet_id);


CREATE INDEX ix_wallet_ownerships_axon_id_state ON identity.wallet_ownerships (axon_id, state) WHERE is_deleted = false;


CREATE INDEX ix_wallet_ownerships_chain_id ON identity.wallet_ownerships (chain_id);


CREATE INDEX ix_wallet_ownerships_created_at ON identity.wallet_ownerships (created_at);


CREATE INDEX ix_wallet_ownerships_state_access_mode ON identity.wallet_ownerships (state, access_mode) WHERE is_deleted = false;


CREATE INDEX ix_wallet_ownerships_wallet_chain_deleted ON identity.wallet_ownerships (wallet_id, chain_id, is_deleted);


CREATE INDEX ix_wallet_ownerships_wallet_id ON identity.wallet_ownerships (wallet_id);


CREATE INDEX ix_wallet_tags_tag ON identity.wallet_tags (tag);


CREATE UNIQUE INDEX ix_wallet_tags_wallet_tag_unique ON identity.wallet_tags (wallet_id, tag);


CREATE INDEX ix_wallets_address ON identity.wallets (address);


CREATE INDEX ix_wallets_chain ON identity.wallets (chain);


CREATE UNIQUE INDEX ix_wallets_chain_address_unique ON identity.wallets (chain, address);


CREATE INDEX ix_wallets_first_seen_at ON identity.wallets (first_seen_at);


CREATE INDEX ix_wallets_last_seen_at ON identity.wallets (last_seen_at);


CREATE INDEX ix_wallets_provider ON identity.wallets (provider);


