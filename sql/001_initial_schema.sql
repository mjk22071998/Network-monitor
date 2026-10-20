-- ==============================================================================
-- Network Observer — Master 3NF Database Schema (PostgreSQL 15+ / 16+ / 17+ / 18+)
-- Document Reference: docs/database_schema_and_ingestion_specification.md
-- Version: 2.2
-- ==============================================================================

BEGIN;

-- ==============================================================================
-- 1. MULTI-TENANCY SCOPE & ORGANIZATIONAL HIERARCHY
-- ==============================================================================

-- Scoped Site entity (Pre-seeded with Site 1 for single-enclave installations)
CREATE TABLE IF NOT EXISTS sites (
    site_id SERIAL PRIMARY KEY,
    site_name VARCHAR(100) NOT NULL UNIQUE,
    facility_code VARCHAR(50),
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Pre-seed default site for single-site deployment
INSERT INTO sites (site_id, site_name, facility_code, description) 
VALUES (1, 'Primary Site Enclave', 'SITE-01', 'Default industrial LAN facility')
ON CONFLICT (site_id) DO NOTHING;

-- Unified Self-Referencing Groups & Subgroups Hierarchy (3NF Compliant)
CREATE TABLE IF NOT EXISTS groups (
    group_id SERIAL PRIMARY KEY,
    site_id INTEGER NOT NULL DEFAULT 1,
    parent_group_id INTEGER,                     -- NULL for top-level Groups; Non-NULL for Subgroups
    group_name VARCHAR(100) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_group_site FOREIGN KEY (site_id) 
        REFERENCES sites(site_id) ON DELETE RESTRICT,
    CONSTRAINT fk_group_parent FOREIGN KEY (parent_group_id) 
        REFERENCES groups(group_id) ON DELETE CASCADE,
    CONSTRAINT uq_site_group_name UNIQUE (site_id, parent_group_id, group_name)
);

-- Pre-seed system fallback group (ID = 1 is reserved for Unassigned assets)
INSERT INTO groups (group_id, site_id, parent_group_id, group_name, description) 
VALUES (1, 1, NULL, 'Unassigned Group', 'System pool for unclassified devices and orphans')
ON CONFLICT (group_id) DO NOTHING;

CREATE INDEX IF NOT EXISTS idx_groups_parent ON groups(parent_group_id);
CREATE INDEX IF NOT EXISTS idx_groups_site ON groups(site_id);

-- ==============================================================================
-- 2. HARDWARE INVENTORY & 4-TIER NETWORK TOPOLOGY
-- ==============================================================================

-- Normalized Device Classifications
CREATE TABLE IF NOT EXISTS device_types (
    type_id SMALLSERIAL PRIMARY KEY,
    type_code VARCHAR(30) NOT NULL UNIQUE        -- 'CORE_ROUTER', 'AGGREGATE_SWITCH', 'ACCESS_SWITCH', 'SERVER', 'PC'
);

INSERT INTO device_types (type_id, type_code) VALUES 
(1, 'CORE_ROUTER'),
(2, 'AGGREGATE_SWITCH'),
(3, 'ACCESS_SWITCH'),
(4, 'SERVER'),
(5, 'PC')
ON CONFLICT (type_id) DO NOTHING;

-- Master Devices Table (Immutable MAC natural key, Mutable IP, Adjacency Tree)
CREATE TABLE IF NOT EXISTS devices (
    device_id SERIAL PRIMARY KEY,
    mac_address VARCHAR(17) NOT NULL UNIQUE,      -- Immutable Hardware Key (XX:XX:XX:XX:XX:XX)
    primary_ip VARCHAR(45) NOT NULL,              -- Mutable IPv4/IPv6 polling address
    display_name VARCHAR(150) NOT NULL,
    type_id SMALLINT NOT NULL,
    group_id INTEGER NOT NULL DEFAULT 1,          -- Defaults to Unassigned Group (1)
    parent_device_id INTEGER,                     -- Self-referencing link: Uplink parent switch/router
    zone VARCHAR(100),                            -- Physical zone (e.g. "Rack B", "Control Room")
    vendor VARCHAR(100),                          -- Hardware vendor inferred from MAC OUI
    model VARCHAR(100),                           -- Model identifier
    is_monitored BOOLEAN DEFAULT TRUE NOT NULL,   -- Polling toggle
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_device_type FOREIGN KEY (type_id) 
        REFERENCES device_types(type_id) ON DELETE RESTRICT,
    CONSTRAINT fk_device_group FOREIGN KEY (group_id) 
        REFERENCES groups(group_id) ON DELETE SET DEFAULT,
    CONSTRAINT fk_device_parent FOREIGN KEY (parent_device_id) 
        REFERENCES devices(device_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_devices_ip ON devices(primary_ip);
CREATE INDEX IF NOT EXISTS idx_devices_mac ON devices(mac_address);
CREATE INDEX IF NOT EXISTS idx_devices_parent ON devices(parent_device_id);
CREATE INDEX IF NOT EXISTS idx_devices_group ON devices(group_id);
CREATE INDEX IF NOT EXISTS idx_devices_monitored ON devices(is_monitored);

-- ==============================================================================
-- 3. NETWORK REACHABILITY & TIME-SERIES SAMPLES (RANGE PARTITIONED)
-- ==============================================================================

-- Global Polling Engine & Monitoring Configuration Singleton
CREATE TABLE IF NOT EXISTS monitoring_config (
    config_id INTEGER PRIMARY KEY DEFAULT 1,
    site_id INTEGER NOT NULL DEFAULT 1,
    reachability_interval_sec INTEGER NOT NULL DEFAULT 5,       -- ICMP & NTP check cadence (seconds)
    reachability_timeout_ms INTEGER NOT NULL DEFAULT 2000,      -- ICMP echo reply timeout (milliseconds)
    health_interval_sec INTEGER NOT NULL DEFAULT 30,             -- Host telemetry polling cadence (seconds)
    health_timeout_ms INTEGER NOT NULL DEFAULT 5000,             -- Host agent HTTP/SNMP timeout (milliseconds)
    cascade_suppression_enabled BOOLEAN NOT NULL DEFAULT TRUE,  -- TRUE = halt downstream pings on switch failure
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_mon_config_site FOREIGN KEY (site_id)
        REFERENCES sites(site_id) ON DELETE RESTRICT,
    CONSTRAINT chk_mon_config_single_row CHECK (config_id = 1)  -- Enforces singleton configuration
);

-- Pre-seed default monitoring configuration
INSERT INTO monitoring_config (config_id, site_id, reachability_interval_sec, reachability_timeout_ms, health_interval_sec, health_timeout_ms, cascade_suppression_enabled)
VALUES (1, 1, 5, 2000, 30, 5000, TRUE)
ON CONFLICT (config_id) DO NOTHING;

-- Normalized Reachability Status Lookup
CREATE TABLE IF NOT EXISTS reachability_status_types (
    status_id SMALLINT PRIMARY KEY,
    status_code VARCHAR(30) NOT NULL UNIQUE      -- 'UNKNOWN', 'REACHABLE', 'UNREACHABLE', 'SUPPRESSED'
);

INSERT INTO reachability_status_types (status_id, status_code) VALUES 
(0, 'UNKNOWN'),
(1, 'REACHABLE'),
(2, 'UNREACHABLE'),
(3, 'SUPPRESSED')
ON CONFLICT (status_id) DO NOTHING;

-- Time-Series Reachability Telemetry (Declaratively Partitioned by Range on recorded_at)
-- NOTE: latency_us and ntp_offset_us are NULLABLE to satisfy the Strict N/A (Never Zero) rule.
CREATE TABLE IF NOT EXISTS reachability_samples (
    sample_id BIGSERIAL,
    device_id INTEGER NOT NULL,
    recorded_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    status_id SMALLINT NOT NULL,
    latency_us INTEGER NULL DEFAULT NULL,         -- Round-Trip Time in microseconds; NULL on timeout/unreachable
    loss_percentage REAL DEFAULT 0.0 NOT NULL,    -- Rolling packet loss percentage
    ntp_offset_us INTEGER NULL DEFAULT NULL,      -- Microsecond clock drift; NULL if NTP unsupported/timeout
    is_suppressed BOOLEAN DEFAULT FALSE NOT NULL, -- TRUE if synthetic ping was suppressed due to parent switch outage
    
    CONSTRAINT fk_reach_device FOREIGN KEY (device_id) 
        REFERENCES devices(device_id) ON DELETE CASCADE,
    CONSTRAINT fk_reach_status FOREIGN KEY (status_id) 
        REFERENCES reachability_status_types(status_id),
    CONSTRAINT pk_reachability_samples PRIMARY KEY (sample_id, recorded_at)
) PARTITION BY RANGE (recorded_at);

-- Catch-all default partition
CREATE TABLE IF NOT EXISTS reachability_samples_default PARTITION OF reachability_samples DEFAULT;

CREATE INDEX IF NOT EXISTS idx_reach_device_time ON reachability_samples(device_id, recorded_at DESC);
CREATE INDEX IF NOT EXISTS idx_reach_time ON reachability_samples(recorded_at);

-- ==============================================================================
-- 4. HOST OPERATING HEALTH & MULTI-VOLUME DISK TELEMETRY (PARTITIONED)
-- ==============================================================================

-- Normalized Health Status Lookup
CREATE TABLE IF NOT EXISTS health_status_types (
    status_id SMALLINT PRIMARY KEY,
    status_code VARCHAR(30) NOT NULL UNIQUE      -- 'AVAILABLE', 'UNAVAILABLE', 'STALE', 'UNSUPPORTED', 'ERROR'
);

INSERT INTO health_status_types (status_id, status_code) VALUES 
(1, 'AVAILABLE'),
(2, 'UNAVAILABLE'),
(3, 'STALE'),
(4, 'UNSUPPORTED'),
(5, 'ERROR')
ON CONFLICT (status_id) DO NOTHING;

-- Time-Series Host Health Samples (Declaratively Partitioned by Range on recorded_at)
CREATE TABLE IF NOT EXISTS health_samples (
    sample_id BIGSERIAL,
    device_id INTEGER NOT NULL,
    recorded_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    status_id SMALLINT NOT NULL,
    cpu_utilization_pct REAL NULL DEFAULT NULL,   -- CPU percent (0.0-100.0); NULL if unsupported
    ram_utilization_pct REAL NULL DEFAULT NULL,   -- RAM percent (0.0-100.0); NULL if unsupported
    net_rx_kbps REAL NULL DEFAULT NULL,           -- Receive throughput in Kbps; NULL if unsupported
    net_tx_kbps REAL NULL DEFAULT NULL,           -- Transmit throughput in Kbps; NULL if unsupported
    temperature_celsius REAL NULL DEFAULT NULL,   -- Hardware temperature; NULL if unsupported
    
    CONSTRAINT fk_health_device FOREIGN KEY (device_id) 
        REFERENCES devices(device_id) ON DELETE CASCADE,
    CONSTRAINT fk_health_status FOREIGN KEY (status_id) 
        REFERENCES health_status_types(status_id),
    CONSTRAINT pk_health_samples PRIMARY KEY (sample_id, recorded_at)
) PARTITION BY RANGE (recorded_at);

-- Catch-all default partition
CREATE TABLE IF NOT EXISTS health_samples_default PARTITION OF health_samples DEFAULT;

CREATE INDEX IF NOT EXISTS idx_health_device_time ON health_samples(device_id, recorded_at DESC);
CREATE INDEX IF NOT EXISTS idx_health_time ON health_samples(recorded_at);

-- Multi-Volume Storage Partition Samples (1NF Normalized Child Table)
CREATE TABLE IF NOT EXISTS disk_volume_samples (
    volume_sample_id BIGSERIAL PRIMARY KEY,
    sample_id BIGINT NOT NULL,
    volume_name VARCHAR(50) NOT NULL,            -- Mount or drive letter (e.g. "C:", "D:", "/var")
    used_percentage REAL NOT NULL,               -- Partition utilized percentage
    total_capacity_gb REAL NOT NULL,             -- Drive capacity in Gigabytes
    free_space_gb REAL NOT NULL                  -- Free space in Gigabytes
);

CREATE INDEX IF NOT EXISTS idx_volume_sample ON disk_volume_samples(sample_id);

-- ==============================================================================
-- 5. PHYSICAL SPATIAL LAYOUT REGISTRY [DEFERRED: FUTURE DEVELOPMENT / MILESTONE 2]
-- ==============================================================================

CREATE TABLE IF NOT EXISTS physical_layouts (
    layout_id SERIAL PRIMARY KEY,
    site_id INTEGER NOT NULL DEFAULT 1,
    layout_slug VARCHAR(100) NOT NULL UNIQUE,     -- Alphanumeric slug matching JSON file (e.g. 'floor_1_lab')
    floor_name VARCHAR(150) NOT NULL,             -- Display label (e.g. '1st Floor Server Lab')
    layout_file_path VARCHAR(255) NOT NULL,       -- Relative path to validated JSON layout file
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_layout_site FOREIGN KEY (site_id) 
        REFERENCES sites(site_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_layouts_site ON physical_layouts(site_id);

-- ==============================================================================
-- 6. SECURITY, ROLE-BASED ACCESS CONTROL (RBAC) & AUDIT
-- ==============================================================================

CREATE TABLE IF NOT EXISTS roles (
    role_id SMALLINT PRIMARY KEY,
    role_code VARCHAR(50) NOT NULL UNIQUE,       -- 'NETWORK_ADMIN', 'NETWORK_ENGINEER', 'IT_SUPPORT'
    role_name VARCHAR(100) NOT NULL,
    description TEXT
);

INSERT INTO roles (role_id, role_code, role_name, description) VALUES 
(1, 'NETWORK_ADMIN', 'Network Admin', 'Full administrative authority, user provisioning, core backbone CRUD, bulk commit gatekeeper, audit trail.'),
(2, 'NETWORK_ENGINEER', 'Network Engineer', 'Topology & connectivity, access switches, subnet discovery sweeps, group/subgroup hierarchy, floorplan calibration, staging uploads.'),
(3, 'IT_SUPPORT', 'IT Support', 'Frontline endpoint management (PCs, Servers, PLCs), operational metadata, switch uplink linking, leaf telemetry diagnostics.')
ON CONFLICT (role_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS users (
    user_id SERIAL PRIMARY KEY,
    site_id INTEGER NOT NULL DEFAULT 1,
    username VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role_id SMALLINT NOT NULL,
    is_active BOOLEAN DEFAULT TRUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_user_site FOREIGN KEY (site_id)
        REFERENCES sites(site_id) ON DELETE RESTRICT,
    CONSTRAINT fk_user_role FOREIGN KEY (role_id) 
        REFERENCES roles(role_id)
);

-- Pre-seed initial administrative account (admin / default Argon2id hash)
-- Password: "Admin@NetworkObserver2026!"
INSERT INTO users (user_id, site_id, username, password_hash, role_id, is_active)
VALUES (1, 1, 'admin', '$argon2id$v=19$m=65536,t=3,p=4$0l6lAQpfBHCuwjnZvBYFwA$r7tpyUj2Tqun7gzxcnPy/5wIurBEMj7NnvzDZG9Pueg', 1, TRUE)
ON CONFLICT (user_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS audit_logs (
    log_id BIGSERIAL PRIMARY KEY,
    user_id INTEGER,                             -- NULL for system background tasks
    site_id INTEGER NOT NULL DEFAULT 1,
    action_code VARCHAR(50) NOT NULL,            -- 'DEVICE_CREATE', 'IP_UPDATE', 'BATCH_COMMIT', 'BATCH_DISCARD'
    target_entity VARCHAR(50) NOT NULL,          -- 'DEVICE', 'GROUP', 'BATCH', 'SITE'
    target_id VARCHAR(50),
    details JSONB,
    ip_address VARCHAR(45) NOT NULL,
    logged_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_audit_user FOREIGN KEY (user_id) 
        REFERENCES users(user_id) ON DELETE SET NULL,
    CONSTRAINT fk_audit_site FOREIGN KEY (site_id)
        REFERENCES sites(site_id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS idx_audit_time ON audit_logs(logged_at DESC);
CREATE INDEX IF NOT EXISTS idx_audit_user ON audit_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_audit_action ON audit_logs(action_code);

-- Tamper-Proofing Triggers: Enforce immutable append-only audit trail via PL/pgSQL
CREATE OR REPLACE FUNCTION fn_audit_logs_immutable()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'SECURITY VIOLATION: audit_logs records are immutable and cannot be updated or deleted.';
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_logs_no_update ON audit_logs;
CREATE TRIGGER trg_audit_logs_no_update
BEFORE UPDATE ON audit_logs
FOR EACH ROW
EXECUTE FUNCTION fn_audit_logs_immutable();

DROP TRIGGER IF EXISTS trg_audit_logs_no_delete ON audit_logs;
CREATE TRIGGER trg_audit_logs_no_delete
BEFORE DELETE ON audit_logs
FOR EACH ROW
EXECUTE FUNCTION fn_audit_logs_immutable();

-- ==============================================================================
-- 7. INDUSTRIAL PERSISTENT INGESTION STAGING PIPELINE [PROVISIONAL]
-- ==============================================================================

CREATE TABLE IF NOT EXISTS import_batches (
    batch_id SERIAL PRIMARY KEY,
    uploaded_by_user_id INTEGER NOT NULL,
    source_file_type VARCHAR(20) NOT NULL,       -- 'SQLITE', 'CSV', 'JSON'
    original_filename VARCHAR(255) NOT NULL,
    total_rows INTEGER DEFAULT 0 NOT NULL,
    status VARCHAR(30) DEFAULT 'STAGED' NOT NULL,-- 'STAGED', 'COMMITTED', 'DISCARDED'
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP NOT NULL,
    committed_at TIMESTAMPTZ NULL,
    
    CONSTRAINT fk_batch_user FOREIGN KEY (uploaded_by_user_id) 
        REFERENCES users(user_id)
);

CREATE TABLE IF NOT EXISTS staged_devices (
    staged_id BIGSERIAL PRIMARY KEY,
    batch_id INTEGER NOT NULL,
    source_row_number INTEGER NOT NULL,          -- Spreadsheet row number for operator traceability
    
    -- Raw unvalidated input values
    raw_mac_address VARCHAR(50),
    raw_primary_ip VARCHAR(50),
    raw_display_name VARCHAR(150),
    raw_device_type VARCHAR(50),
    raw_group_path VARCHAR(255),                 -- Supports nested path e.g. "Operations / Subgroup A"
    raw_parent_identifier VARCHAR(50),           -- Target parent IP or MAC
    raw_zone VARCHAR(100),
    raw_vendor VARCHAR(100),
    raw_model VARCHAR(100),
    is_monitored BOOLEAN DEFAULT TRUE NOT NULL,
    
    -- Automated pre-resolution foreign keys
    resolved_type_id SMALLINT,
    resolved_group_id INTEGER,
    resolved_parent_device_id INTEGER,
    
    -- Validation state machine
    validation_status VARCHAR(30) DEFAULT 'INCOMPLETE' NOT NULL, -- 'VALID', 'INCOMPLETE', 'CONFLICT'
    validation_errors TEXT,                      -- Detailed JSON or comma-separated error notes
    is_user_modified BOOLEAN DEFAULT FALSE NOT NULL, -- Set to TRUE when operator manually edits inline
    
    CONSTRAINT fk_staged_batch FOREIGN KEY (batch_id) 
        REFERENCES import_batches(batch_id) ON DELETE CASCADE,
    CONSTRAINT fk_staged_type FOREIGN KEY (resolved_type_id) 
        REFERENCES device_types(type_id) ON DELETE SET NULL,
    CONSTRAINT fk_staged_group FOREIGN KEY (resolved_group_id) 
        REFERENCES groups(group_id) ON DELETE SET NULL,
    CONSTRAINT fk_staged_parent FOREIGN KEY (resolved_parent_device_id) 
        REFERENCES devices(device_id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_staged_batch_status ON staged_devices(batch_id, validation_status);

COMMIT;
