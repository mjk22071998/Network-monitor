# Network Observer — Living Progress Tracker & Implementation Checklist
**Document Version:** 2.2  
**Project:** Network Observer (Industrial LAN Telemetry & Topology Management Platform)  
**Classification:** Restricted — Air-Gapped Industrial Enclave (SCADA / NOC Deployment)  
**Location:** `d:\FullStackProjects\Network monitor`  
**Last Updated:** 18 October 2026  
**Related Master Roadmap:** [`project_implementation_roadmap_and_phases.md`](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md)

---

## 1. Executive Status Dashboard

| Metric | Current Status | Notes |
| :--- | :---: | :--- |
| **Current Milestone** | **Milestone 4: Fullstack Hardening & Unified Air-Gapped Release** | All Phases 0 through 9 COMPLETE & VERIFIED. Ready for comprehensive manual testing. |
| **Active Phase** | **Phase 9: Fullstack Integration, Unified Serving & Hardening** | 100% COMPLETE & VERIFIED. Ready for manual test execution. |
| **Specifications Status** | **100% COMPLETE & LOCKED** | All master specification documents approved. |
| **Database Engine** | **PostgreSQL 18** | 3NF DDL, Range Partitioning, PL/pgSQL Triggers verified. |
| **Backend Stack** | **C# (.NET 10) Web API + EF Core 10** | 4-Tier Layered Architecture, Npgsql, Async Ping, Zero Raw SQL, SOLID & GoF Patterns. |
| **Frontend Stack** | **Vue 3 + Vite + Cytoscape.js + ECharts** | Pinia, fine-grained proxy reactivity, Cyber-Tactical theme. |
| **Memory Architecture**| **500 MB Total System RAM Allowance** | Explicit memory budget, `markRaw()` on canvas, `shallowRef()` on telemetry batches. Verified at ~265MB working set. |
| **Overall Completion** | **100% (Phases 0 through 9 Complete & Verified)** | Ready for end-to-end interactive manual operator testing. |

## 2. Mandatory Verification Protocol

> [!IMPORTANT]
> **Strict Step-by-Step Verification Mandate**  
> Every task in this checklist requires **verifiable proof** before being marked as completed (`[x]`). We will execute verification commands (terminal outputs, build logs, HTTP curl tests, browser checks) and review results together before advancing to the next step. No steps will be grouped or skipped without explicit demonstration and approval.

---

## 3. Phase-by-Phase Execution Checklist

### Phase 0: Workspace Scaffolding & Master Build Setup
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 0](file:///d:/FullStackProjects/Network%20monitor/docs/project_implementation_roadmap_and_phases.md#phase-0-workspace-scaffolding--master-build-pipeline)  
**Architecture Reference:** [`software_architecture_and_libraries_specification.md`](file:///d:/FullStackProjects/Network%20monitor/docs/software_architecture_and_libraries_specification.md)

- [x] **0.1 Git Repository Initialization & Baseline Commit**:
  - [x] Execute `git init` in `d:\FullStackProjects\Network monitor`.
  - [x] Author root `.gitignore` (ignoring `build/`, `bin/`, `obj/`, `dist/`, `node_modules/`, `*.log`, `.env`).
  - [x] Stage and commit master specification suite and prototypes.
  - *Verification Proof*: Executed `git status` (clean tree) and `git log -1` (`71bda1e` — 11 files, 5804 insertions).
- [x] **0.2 .NET 8 4-Tier Solution & Directory Scaffolding**:
  - [x] Create root solution `NetworkObserver.slnx` via `dotnet new sln`.
  - [x] Create `NetworkObserver.Api` (ASP.NET Core Web API with controllers).
  - [x] Create `NetworkObserver.Application` (Class Library).
  - [x] Create `NetworkObserver.Domain` (Class Library).
  - [x] Create `NetworkObserver.Infrastructure` (Class Library).
  - [x] Configure project references enforcing unidirectional dependency rules.
  - [x] Scaffold `/frontend` directory structure (`src/api`, `src/types`, `src/stores`, `src/features`, `src/components/common`).
  - *Verification Proof*: Executed `dotnet sln NetworkObserver.slnx list` (all 4 projects loaded) and `dotnet build` (`0 Warning(s), 0 Error(s)`). Directory layout verified.
- [x] **0.3 Minimal ASP.NET Core API Compilation & Health Check**:
  - [x] Install core NuGet packages (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Isopoh.Cryptography.Argon2`, `FluentValidation`, `Microsoft.EntityFrameworkCore.Design`).
  - [x] Configure minimal health check endpoint `GET /api/v1/health` returning `{"status": "UP", "version": "2.2"}`.
  - [x] Compile solution via `dotnet build` (`0 Warning(s), 0 Error(s)`).
  - *Verification Proof*: Ran `dotnet run` on port 5000 and executed `curl.exe -i http://localhost:5000/api/v1/health` verifying `HTTP/1.1 200 OK` and `{"status":"UP","version":"2.2","timestamp":"..."}` response.
- [x] **0.4 Frontend Project Initialization (Vue 3 + Vite + Tailwind + Cytoscape + ECharts)**:
  - [x] Initialize Vue 3 TypeScript project in `/frontend` via `npm.cmd`.
  - [x] Install Tailwind CSS, `@tailwindcss/forms`, Lucide icons, Pinia, Cytoscape.js, and Apache ECharts.
  - [x] Configure `vite.config.ts` with API proxy (`/api` $\rightarrow$ `http://localhost:5000`).
  - *Verification Proof*: Executed `npm.cmd run build` (`vue-tsc && vite build` transformed 1533 modules, compiled `/frontend/dist` in 6.82s with zero errors).

---

### Phase 1: PostgreSQL 18 Database Engine & 3NF Schema Initialization
**Status:** `COMPLETE`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 1](file:///d:/FullStackProjects/Network%20monitor/docs/project_implementation_roadmap_and_phases.md#phase-1-postgresql-18-database-engine--3nf-schema-initialization)  
**Schema Reference:** [`database_schema_and_ingestion_specification.md` § 4](file:///d:/FullStackProjects/Network%20monitor/docs/database_schema_and_ingestion_specification.md#L111)

- [x] **1.1 Database Migration Scripts**:
  - [x] Write `001_initial_schema.sql` with 3NF DDL for all core tables.
  - [x] Implement declarative range partitioning for `reachability_samples` by `recorded_at`.
  - [x] Implement declarative range partitioning for `health_samples` by `recorded_at`.
  - [x] Create default partitions (`reachability_samples_default`, `health_samples_default`).
  - *Verification Proof*: Executed `sql/001_initial_schema.sql` on PostgreSQL 18 (`network_observer`). Verified 18 tables and default partitions exist via `\d+ reachability_samples` and `\d+ health_samples`.
- [x] **1.2 Non-Repudiation Triggers**:
  - [x] Author `fn_audit_logs_immutable()` PL/pgSQL function.
  - [x] Attach `trg_audit_logs_no_update` and `trg_audit_logs_no_delete` triggers to `audit_logs`.
  - *Verification Proof*: Inserted row into `audit_logs`; subsequent `UPDATE` and `DELETE` attempts were immediately aborted by PostgreSQL with: `ERROR: SECURITY VIOLATION: audit_logs records are immutable and cannot be updated or deleted.`
- [x] **1.3 Master Seed Data**:
  - [x] Pre-seed Site 1 (`Primary Site Enclave`, `SITE-01`).
  - [x] Pre-seed Group 1 (`Unassigned Group`).
  - [x] Pre-seed Roles (1: `NETWORK_ADMIN`, 2: `NETWORK_ENGINEER`, 3: `IT_SUPPORT`).
  - [x] Pre-seed Device Types (1: `CORE_ROUTER`, 2: `AGGREGATE_SWITCH`, 3: `ACCESS_SWITCH`, 4: `SERVER`, 5: `PC`).
  - [x] Pre-seed Monitoring Config singleton row 1 (5s reachability, 30s health, cascade suppression enabled).
  - [x] Pre-seed Reachability & Health Status lookup tables.
  - [x] Pre-seed initial `admin` account with Argon2id hash (`$argon2id$...`).
  - *Verification Proof*: Executed multi-table SELECT query via `psql` validating row existence for all 8 seeded entities.
- [x] **1.4 C# EF Core Database Context & Entity Configurations**:
  - [x] Implement Domain entity classes in `NetworkObserver.Domain` with zero external dependencies.
  - [x] Implement `NetworkObserverDbContext` with Fluent API mappings in `NetworkObserver.Infrastructure`.
  - [x] Configure PostgreSQL connection string in `NetworkObserver.Api/appsettings.json`.
  - [x] Register DbContext in ASP.NET Core DI container and verify connection.
  - *Verification Proof*: Compiled entire 4-tier solution (`0 Warning(s), 0 Error(s)`). Booted Kestrel server on port 5000 and queried `GET /api/v1/health` via `curl.exe` returning `200 OK` with `{"status":"UP","database":{"isConnected":true,"databaseEngine":"PostgreSQL 18","siteCount":1,"groupCount":1,"deviceCount":0,"userCount":1,"queryLatencyMs":40}}`. Zero raw SQL queries; 100% type-safe EF Core LINQ.

---

### Phase 2: Security, Argon2id & Session Authentication Engine
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 2](file:///d:/FullStackProjects/Network%20monitor/docs/project_implementation_roadmap_and_phases.md#phase-2-security-argon2id--session-authentication-engine)  
**Security Reference:** [`security_and_rbac_specification.md`](file:///d:/FullStackProjects/Network%20monitor/docs/security_and_rbac_specification.md)

- [x] **2.1 Argon2id Password Cryptography**:
  - [x] Implement Argon2id hashing wrapper with parameters: $m=64\text{MB}, t=3, p=4$ using `Isopoh.Cryptography.Argon2`.
  - [x] Implement constant-time password verification (`Argon2.Verify`).
  - [x] Implement dummy hashing pass on deactivated accounts (`is_active = false`) to defeat timing side-channel attacks.
  - *Verification Proof*: Authored `Argon2idPasswordHasherTests.cs`. Verified PHC format (`$argon2id$v=19$m=65536,t=3,p=4$`), verified constant-time verification passes on correct password and rejects wrong password, and verified live pre-seeded `admin` password against PostgreSQL (`Admin@NetworkObserver2026!`). All tests passed.
- [x] **2.2 Cryptographic Session Manager**:
  - [x] Implement 256-bit CSPRNG token generator (`ISessionTokenFactory` / `SessionTokenFactory` with `obs_tok_` prefix).
  - [x] Implement thread-safe in-memory session cache with `ConcurrentDictionary` (`ISessionManager` / `SessionManager` Singleton).
  - [x] Enforce 8-hour shift lifetime and 30-minute idle inactivity timeout.
  - [x] Implement session revocation and user-wide revocation.
  - *Verification Proof*: Authored `SessionManagementTests.cs`. Verified 256-bit entropy token generation, uniqueness, 8h absolute expiry, sliding idle window refresh, idle timeout eviction, single session revocation, and multi-session revocation for user ID. All 13 xUnit tests passed (`Total: 13, Passed: 13, Failed: 0`).
- [x] **2.3 ASP.NET Core RBAC Guard Middleware**:
  - [x] Intercept requests; extract session from `observer_session` cookie or `Authorization: Bearer`.
  - [x] Enforce route permissions per Master RBAC Matrix (`401 Unauthorized` vs `403 Forbidden`).
  - [x] Attach authenticated `ClaimsPrincipal` with user identity, role, and session token.
  - *Verification Proof*: Authored `AuthorizeRoleFilterTests.cs` and `SessionAuthenticationMiddleware.cs`. Verified unauthenticated requests return `401 Unauthorized` with standard error envelope; verified insufficient roles return `403 Forbidden` with standard error envelope; verified matching roles permit execution. All tests passed.
- [x] **2.4 Authentication API Endpoints**:
  - [x] `POST /api/v1/auth/login`: Authenticate credentials, set Secure HttpOnly cookie, return numeric IDs and 256-bit token.
  - [x] `POST /api/v1/auth/logout`: Revoke active session and clear cookie (`204 No Content`).
  - [x] `GET /api/v1/auth/me`: Return caller identity, role profile, and active status.
  - [x] `PUT /api/v1/auth/password`: Authenticated self-service password update with complexity, reuse, and session invalidation guards.
  - *Verification Proof*: Executed 26 automated unit/integration tests (`Total: 26, Passed: 26, Failed: 0`). Verified end-to-end against live Kestrel server via `curl.exe`: login sets cookie & token, `GET /auth/me` verifies identity, logout revokes session (`204`), subsequent access rejected with `401`, password update rejects weak/reused passwords and revokes active sessions.
- [x] **2.5 User Administration API (Admin Only)**:
  - [x] `GET /api/v1/users`: List users with string `role` and `site_name`.
  - [x] `POST /api/v1/users`: Pre-provision operator accounts (Zero self-registration).
  - [x] `GET /api/v1/users/{id}` & `PUT /api/v1/users/{id}`: Profile updates & admin password reset.
  - [x] `DELETE /api/v1/users/{id}`: Safe deactivation; prevent self-deletion (`409`).
  - *Verification Proof*: Authored `UserServiceTests.cs` (12 unit tests). Total automated test suite expanded to 38 tests, all passing (`Total: 38, Passed: 38, Failed: 0`). Executed live verification against Kestrel server: admin authenticated `POST /api/v1/users` created operator (`user_id = 2`, `eng_carter`), `GET /api/v1/users/2` returned detail, `PUT /api/v1/users/2` updated role, `DELETE /api/v1/users/1` self-deletion was rejected with `409 Conflict`, and `DELETE /api/v1/users/2` returned `204 No Content`. SPA static hosting and fallback configured in `Program.cs`.

---

### Phase 3: Core Enclave Entities & Device Inventory CRUD
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 3](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-3-core-enclave-entities--device-inventory-crud)  
**API Reference:** [`api_contracts_specification.md` § 4, 5, 7](file:///d:/FullStackProjects/Network%20monitor/api_contracts_specification.md)

- [x] **3.1 Enclave Site API**:
  - [x] `GET /api/v1/site`: Return single site enclave data (`site_id = 1`) for all authenticated roles.
  - [x] `PUT /api/v1/site`: Admin update of site metadata with strict boundary validation (name, code, desc).
  - *Verification Proof*: Authored `SiteServiceTests.cs` (10 unit tests). Entire test suite expanded to 48 tests passing (`Total: 48, Passed: 48, Failed: 0`). Executed live verification against Kestrel server: admin authenticated `GET /api/v1/site` returned initial enclave (`SITE-01`), admin `PUT /api/v1/site` updated metadata (`Reactor Unit 4 Control Facility`, `FAC-R4-SCADA`), unauthenticated requests were rejected with `401 Unauthorized`, and engineer role `PUT` requests were rejected with `403 Forbidden`. Persistence in PostgreSQL 18 verified.
- [x] **3.2 Nested Group Hierarchy API**:
  - [x] `GET /api/v1/groups`: Nested recursive tree with **`children: [...]`** attribute and direct device counts.
  - [x] `POST /api/v1/groups`: Create new group/subgroup with parent validation.
  - [x] `PUT /api/v1/groups/{id}`: Rename/move group; circular dependency prevention and Root Group 1 protection.
  - [x] `DELETE /api/v1/groups/{id}`: Fallback reassigning member devices to Group 1 (Unassigned) and reparenting child subgroups.
  - *Verification Proof*: Authored `GroupServiceTests.cs` (12 unit tests). Total automated test suite expanded to 60 tests passing (`Total: 60, Passed: 60, Failed: 0`). Executed live verification against Kestrel server: admin authenticated `POST /api/v1/groups` created parent group `Operations` (ID 2) and child subgroups `Assembly Line A` (ID 3) and `Assembly Line B` (ID 4); `GET /api/v1/groups` returned full hierarchical tree with `children: [...]`; reparenting Root Group 1 was rejected with `409 Conflict`; cyclic dependency (setting Operations parent to its child Assembly Line A) was rejected with `422 Unprocessable Entity`; deleting Root Group 1 was rejected with `409 Conflict`; deleting child group safely reassigned member devices to Group 1 and returned `200 OK`; IT Support role attempts to mutate groups were rejected with `403 Forbidden`.
- [x] **3.3 Device Inventory CRUD API**:
  - [x] `GET /api/v1/devices`: Paginated device records (10 schema fields) with filtering and alternative MAC lookup (`?mac=...`).
  - [x] `POST /api/v1/devices`: Create device; tiered equipment role authorization (Admin $\rightarrow$ Backbone; Engineer $\rightarrow$ Switches; Support $\rightarrow$ Leaf PCs).
  - [x] `GET /api/v1/devices/{id}`: Single device profile with parent link details.
  - [x] `PUT /api/v1/devices/{id}`: Update device; immutable MAC address enforcement (`422 DEVICE_MAC_IMMUTABLE`).
  - [x] `DELETE /api/v1/devices/{id}`: Safe orphan deletion setting child `parent_device_id = NULL`.
  - *Verification Proof*: Authored `DeviceServiceTests.cs` (14 unit tests). Entire automated test suite expanded to 74 tests passing (`Total: 74, Passed: 74, Failed: 0`). Executed live verification against Kestrel server: admin provisioned Core Router (ID 1), Access Switch (ID 2), and Leaf PLC (ID 3); `GET /devices` verified pagination and MAC lookup (`?mac=...`); `PUT /devices/{id}` rejected MAC mutation with `422 Unprocessable Entity` and permitted valid attribute update; deleting Access Switch safely orphaned child PLC (`parent_device_id = null`) and returned `200 OK` with orphaned child ID list; tiered RBAC verified live: IT Support creating a Core Router was rejected with `403 Forbidden`, while IT Support creating a Leaf PC succeeded with `201 Created`.
- [x] **3.4 Global Polling Configuration API**:
  - [x] `GET /api/v1/monitoring/config`: Return singleton configuration for all authenticated roles.
  - [x] `PUT /api/v1/monitoring/config`: Admin/Engineer update of polling cadences with strict boundary validation (1s–60s reachability, 10s–300s health).
  - *Verification Proof*: Authored `MonitoringConfigServiceTests.cs` (14 unit tests). Entire test suite expanded to 88 tests passing (`Total: 88, Passed: 88, Failed: 0`). Executed live verification against Kestrel server: admin `GET /api/v1/monitoring/config` verified initial 5s/30s cadences; admin `PUT` successfully updated cadences (10s reachability, 60s health, cascade suppression toggle); out-of-bounds cadences were rejected with `400 Bad Request`; IT Support role `PUT` was rejected with `403 Forbidden` while `GET` was permitted (`200 OK`); persistence in PostgreSQL 18 verified. Single-site configuration restored. Phase 3 complete.

---

### Phase 4: High-Performance Multithreaded Telemetry Poller
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 4](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-4-high-performance-multithreaded-telemetry-poller)  
**SRS Reference:** [`master_software_requirements_specification.md` § 5.4–5.6](file:///d:/FullStackProjects/Network%20monitor/master_software_requirements_specification.md)

- [x] **4.1 Native Raw Socket ICMP Engine**:
  - [x] Implement native ICMP polling loop via `System.Net.NetworkInformation.Ping` with high-resolution `Stopwatch` (zero subprocess spawning).
  - [x] Compute sub-millisecond round-trip time (`latency_us`) and packet loss percentage.
  - [x] Enforce Strict N/A rule: write `NULL` for missing/timed-out latency (never `0`).
  - [x] Bounded concurrency pool (`MaxDegreeOfParallelism = 50`) honoring the 500MB memory budget.
  - *Verification Proof*: Authored `TelemetryServiceTests.cs` (8 unit tests). Entire automated test suite expanded to 96 tests passing (`Total: 96, Passed: 96, Failed: 0`). Executed live verification against Kestrel server on `http://localhost:5000`: `POST /api/v1/telemetry/poll` executed non-blocking parallel sweeps across monitored devices; confirmed Strict N/A Rule with `latency_us: null` on unreachable nodes; provisioned local loopback host (`127.0.0.1`) and verified positive sub-millisecond measurement (`latency_us: 1441`, `status_code: REACHABLE`, `loss_percentage: 0`); verified `GET /api/v1/telemetry/summary`, `GET /api/v1/devices/{id}/telemetry/live`, and `GET /api/v1/devices/{id}/telemetry/history` adhering strictly to `api_contracts_specification.md` Section 7.
- [x] **4.2 UDP NTP Clock Drift Engine**:
  - [x] Implement native asynchronous 48-byte UDP NTP client (RFC 4330 / RFC 5905) querying remote devices on UDP port 123 (`INtpStrategy`, `UdpSocketNtpStrategy`, `INtpProbeFactory`, `NtpProbeFactory`).
  - [x] Compute microsecond clock drift ($\theta = \frac{(T_2 - T_1) + (T_3 - T_4)}{2}$) as signed 32-bit `ntp_offset_us`.
  - [x] Enforce Strict N/A rule: write `NULL` for missing, unconfigured, or timed-out NTP metrics (never `0`).
  - *Verification Proof*: Authored `ExecuteReachabilitySweepAsync_ProbesMonitoredDevices_AndCalculatesNtpDrift_StrictNARule` in `TelemetryServiceTests.cs`. Entire automated test suite expanded to 97 tests passing (`Total: 97, Passed: 97, Failed: 0`). Executed live verification against Kestrel server on `http://localhost:5000`: `POST /api/v1/telemetry/poll` queried reachable nodes, confirmed `ntp_offset_us: null` on nodes without active NTP service (never `0`), and verified `GET /api/v1/devices/{id}/telemetry/live` schema compliance.
- [x] **4.3 Cascading Failure Suppression Circuit**:
  - [x] In-memory switch-device DAG adjacency traversal (`ICascadeSuppressionCircuit`, `CascadeSuppressionCircuit`).
  - [x] Automatically mark downstream children `SUPPRESSED` (`status_id = 3`, `is_suppressed = true`, `latency_us = null`, `ntp_offset_us = null`) on upstream parent failure (FR-CAS-01 - FR-CAS-04).
  - [x] Halt synthetic ICMP and NTP packets to downstream suppressed nodes, eliminating network packet storms and PLC tripping.
  - [x] Respect `monitoring_config.enable_cascade_suppression` toggle; automatic recovery when parent recovers.
  - *Verification Proof*: Authored `ExecuteReachabilitySweepAsync_WhenUpstreamSwitchFails_ImmediatelySuppressesDownstreamNodes_WithoutSyntheticPings` and `ExecuteReachabilitySweepAsync_WhenCascadeSuppressionDisabled_PollsAllDevicesDirectly` in `TelemetryServiceTests.cs`. Entire automated test suite expanded to 99 tests passing (`Total: 99, Passed: 99, Failed: 0`). Executed live verification against Kestrel server: provisioned Root Switch (offline) and Downstream Child PLC; `POST /api/v1/telemetry/poll` recorded child node as `SUPPRESSED` with `latency_us: null` and zero synthetic ping transmissions; `GET /api/v1/devices/{id}/telemetry/live` returned `status_code: "SUPPRESSED"` and `is_suppressed: true`; `GET /api/v1/telemetry/summary` reflected `suppressed_count: 1`. Cleaned up test assets.
- [x] **4.4 Host Health Metrics Collector**:
  - [x] Decoupled host health collector evaluating endpoints on port 9182 (`IHostHealthStrategy`, `HttpAgentHealthStrategy`, `IHostHealthProbeFactory`, `HostHealthProbeFactory`).
  - [x] Collects CPU %, RAM %, network throughput (Rx/Tx Kbps), temperature, and multi-volume partition samples (`disk_volume_samples`).
  - [x] Enforces equipment tier rules: network infrastructure devices (Routers/Switches) classified as `UNSUPPORTED` (`status_id = 4`); offline endpoints classified as `UNAVAILABLE` (`status_id = 2`).
  - [x] Strict N/A Rule: all metrics recorded as `NULL` on failure/unsupported (never `0`).
  - [x] Range-partitioned persistence via `IHealthRepository` / `HealthRepository`.
  - *Verification Proof*: Authored `ExecuteHealthSweepAsync_ProbesMonitoredEndpoints_AndPersistsMultiVolumeDiskStorage` and `GetLiveTelemetryAsync_WhenHealthSampleExists_PopulatesHostLayerAndDiskVolumes` in `TelemetryServiceTests.cs`. Entire automated test suite expanded to 101 tests passing (`Total: 101, Passed: 101, Failed: 0`). Executed live verification against Kestrel server: `POST /api/v1/telemetry/health/poll` executed decoupled health sweeps across provisioned enclave devices; `GET /api/v1/devices/1/telemetry/live` verified `UNSUPPORTED` classification for Core Router with null metrics; `GET /api/v1/devices/3/telemetry/live` verified `UNAVAILABLE` classification for Leaf endpoint with null metrics; `GET /api/v1/telemetry/summary` verified `host_health` HUD object aggregation.
- [x] **4.5 Micro-Batch Ingestion**:
  - [x] Continuous background scheduling engine (`TelemetryPollingBackgroundService : BackgroundService`).
  - [x] Independent cadences dynamically read from database: 5-second ICMP reachability and 30-second host health.
  - [x] Micro-batch persistence into PostgreSQL range-partitioned tables (`reachability_samples`, `health_samples`, `disk_volume_samples`).
  - [x] Non-reentrant execution gate via `SemaphoreSlim(1, 1)` preventing drift or concurrency stampedes.
  - *Verification Proof*: Registered as ASP.NET Core `IHostedService` in `Program.cs`. Verified live execution against running Kestrel server: daemon autonomously executed recurring 5-second reachability and 30-second host health sweeps, writing time-stamped partitioned micro-batches directly to PostgreSQL 18 without operator intervention.
- [x] **4.6 Telemetry & Topology API Endpoints**:
  - [x] `GET /api/v1/topology`: Return complete hierarchical graph with 2-Branch Layout Guarantee (`branch_left` / `branch_right` with `branch_index`).
  - [x] `GET /api/v1/telemetry/{id}` & `GET /api/v1/devices/{id}/telemetry/live`: Return slide-over inspector node details with decoupled `network_layer` and `host_layer`.
  - [x] `GET /api/v1/telemetry/{id}/history` & `GET /api/v1/devices/{id}/telemetry/history`: Return raw time-series reachability samples within time window.
  - *Verification Proof*: Authored `GetTopologyTreeAsync_ReturnsTwoBranchAccessSwitchHierarchy` in `TelemetryServiceTests.cs`. Entire automated test suite expanded to 102 tests passing (`Total: 102, Passed: 102, Failed: 0`). Executed live verification against Kestrel server: provisioned Access Switch with alternating leaf endpoints; verified `GET /api/v1/topology` returned 2-branch partitioned layout with alternating `branch_index` values (0, 1, 2...); verified `GET /api/v1/telemetry/1` returned slide-over inspector payload; verified `GET /api/v1/telemetry/1/history` returned time-series history. Cleaned up test assets. Phase 4 complete.

---

### Phase 5: Subnet Discovery Sweep & Operational Diagnostics
**Status:** `COMPLETED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 5](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-5-subnet-discovery-sweep--operational-diagnostics)

- [x] **5.1 Asynchronous Subnet Discovery Worker**:
  - [x] `POST /api/v1/discovery/scan`: Dispatch async sweep for CIDR (max `/24`).
  - [x] Enforce max 1 concurrent scan per site (`409 Conflict`).
  - [x] 10ms packet pacing between probes to prevent network congestion.
  - [x] Ping sweep, native OS ARP cache read (`SendARP`), MAC OUI typing heuristics (`Dell`, `HP`, `Cisco`, `Lenovo`, `Apple`, etc.).
- [x] **5.2 Discovery Status API**:
  - [x] `GET /api/v1/discovery/scan/{id}`: Return progress % and discovered assets list.
  - [x] `POST /api/v1/discovery/scan/{id}/cancel`: Cancellation hook with immediate lock release.
- [x] **5.3 Device Maintenance Mode Engine**:
  - [x] `PUT /api/v1/devices/{id}/maintenance`: Toggle `is_monitored = false`.
  - [x] Turn node slate-gray (`UNKNOWN`) with `latency_us = null` (Strict N/A Rule).
  - [x] Enforce role check: `IT_SUPPORT` restricted to leaf endpoints (`type_id` 4, 5); Switches and Routers require `NETWORK_ADMIN` / `NETWORK_ENGINEER`.
  - [x] Log immutable event to PostgreSQL `audit_logs` table.
- [x] **5.4 Automated Test Suite**:
  - [x] Authored `DeviceMaintenanceTests.cs` (7 unit tests).
  - [x] Authored `SubnetDiscoveryServiceTests.cs` (17 unit tests).
  - [x] Total suite: **126 tests passed, 0 failed, 0 skipped**.
  - [x] Live end-to-end PowerShell/curl verification on `http://localhost:5000` passed.

---

### Phase 6: Low-Memory Streaming Bulk Ingestion Pipeline
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 6](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-6-low-memory-streaming-bulk-ingestion-pipeline)  
**Schema Reference:** [`database_schema_and_ingestion_specification.md` § 6](file:///d:/FullStackProjects/Network%20monitor/database_schema_and_ingestion_specification.md#L518)

- [x] **6.1 Zero-RAM-Spike Streaming Upload**:
  - [x] `POST /api/v1/ingest/upload`: Multipart upload handler with 64KB ring buffer.
  - [x] Streaming row parsing via line reader without loading entire payload into memory.
  - [x] 500-record chunking with `ChangeTracker.Clear()` to bound RSS memory $< 15\text{MB}$.
  - [x] Ingest to persistent `staged_devices` and `import_batches` tables.
- [x] **6.2 Automated Pre-Resolution**:
  - [x] Resolve parent IP/MAC/name strings to `parent_device_id` (including intra-batch pre-resolution).
  - [x] Resolve nested group paths to `group_id`.
  - [x] Device type heuristic typing (`type_id`) and MAC OUI vendor lookup.
  - [x] Flag rows: `VALID`, `INCOMPLETE`, `CONFLICT`.
- [x] **6.3 Staging Batches & Keyset Grid API**:
  - [x] `GET /api/v1/ingest/batches`: List persistent batches.
  - [x] `GET /api/v1/ingest/batches/{id}/records`: Keyset paginated (`LIMIT 50`) with status filtering.
- [x] **6.4 Inline Cell Editing**:
  - [x] `PUT /api/v1/ingest/records/{id}`: In-place edit with instant re-validation and status update.
- [x] **6.5 Atomic Commit Gatekeeper (Admin Only)**:
  - [x] `POST /api/v1/ingest/batches/{id}/commit`: Atomic insert into `devices` wrapped in EF Core Retrying Execution Strategy.
  - [x] Support `commit_valid_only: true`.
  - [x] Transactional rollback on error and audit log generation (`BATCH_COMMIT`).
- [x] **6.6 Batch Discard**:
  - [x] `DELETE /api/v1/ingest/batches/{id}`: Purge staging records with audit logging (`BATCH_DISCARD`).
- [x] **6.7 Automated Test Suite & Live Verification**:
  - [x] Authored `IngestServiceTests.cs` (12 unit tests). Total automated test suite: **135 tests passing, 0 failures, 0 skipped**.
  - [x] Live end-to-end PowerShell/curl verification on `http://localhost:5000` passed:
    - Uploaded CSV with 1 valid and 1 incomplete row.
    - Verified staged batch and paginated records.
    - Updated incomplete row inline, verifying revalidation to `VALID`.
    - Committed valid records atomically into `devices` table.
    - Verified new devices queryable and vendor auto-resolved from MAC OUI.
    - Verified batch discard and cleanup. Phase 6 complete.

---

### Phase 7: Security Audit Trail Engine & Non-Repudiation
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 7](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-7-security-audit-trail-engine--non-repudiation)

- [x] **7.1 Central Audit Logging Service**:
  - [x] Implement `IAuditService` and `AuditService` with standard JSONB formatting and extraction.
  - [x] Map all 20 action codes (`AUTH_LOGIN`, `AUTH_LOGOUT`, `AUTH_FAILURE`, `PASSWORD_CHANGE`, `USER_CREATE`, `USER_UPDATE`, `USER_DELETE`, `DEVICE_CREATE`, `DEVICE_UPDATE`, `DEVICE_DELETE`, `MAINTENANCE_TOGGLE`, `GROUP_CREATE`, `GROUP_UPDATE`, `GROUP_DELETE`, `SITE_UPDATE`, `CONFIG_UPDATE`, `SCAN_TRIGGER`, `SCAN_CANCEL`, `BATCH_UPLOAD`, `BATCH_EDIT`, `BATCH_COMMIT`, `BATCH_DISCARD`).
  - [x] Wire audit logging into all mutation controllers and application services.
- [x] **7.2 Trigger Tamper Verification**:
  - [x] Verify `trg_audit_logs_no_update` and `trg_audit_logs_no_delete` abort tampering with `SECURITY VIOLATION`.
  - [x] Permit foreign key cascade setting `user_id = NULL` on user deletion while strictly preserving immutable audit facts.
  - [x] Live automated integration tests in `AuditTriggerTamperTests.cs` (tamper UPDATE and tamper DELETE aborted).
- [x] **7.3 Immutable Audit Query API (Admin Only)**:
  - [x] `GET /api/v1/audit/logs`: Paginated log query with ISO 8601 date range, action code, and user filters.
  - [x] Enforce strict RBAC with `[AuthorizeRole(RoleType.NetworkAdmin)]` (`403 Forbidden` for non-admins).
- [x] **7.4 Automated Test Suite & Live Verification**:
  - [x] Authored `AuditServiceTests.cs` (10 unit tests) and `AuditTriggerTamperTests.cs` (2 database integration tests).
  - [x] Total automated test suite: **147 tests passing, 0 failures, 0 skipped**.
  - [x] Live end-to-end PowerShell/curl verification on `http://localhost:5000` passed:
    - Admin login and health check.
    - Provisioned test operator and generated `USER_CREATE` audit event.
    - Queried `GET /api/v1/audit/logs?limit=10` and validated pagination metadata.
    - Verified filtering by `action_code=USER_CREATE` and `user_id=1`.
    - Tested RBAC: non-admin access to audit logs returned `403 Forbidden`.
    - Cleaned up test operator with successful foreign key audit log retention. Phase 7 complete.

---

### Phase 8: Cyber-Tactical Vue 3 Frontend Construction
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 8](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-8-cyber-tactical-vue-3-frontend-construction)  
**UI Guidelines:** [`ui_guidelines_and_design_system.md`](file:///d:/FullStackProjects/Network%20monitor/ui_guidelines_and_design_system.md)

- [x] **8.1 Cyber-Tactical Design System Setup**:
  - [x] Configure Tailwind theme (`#050811`, `#070b16`, cyan accents, monospace typography).
  - [x] Set up layout zones (Header, HUD, Main Canvas, Slide-Over, Tables).
- [x] **8.2 Memory-Optimized Pinia Stores**:
  - [x] Use `shallowRef()` for all tabular datasets, inventory, metrics, staging records, and audit logs.
  - [x] Bounded browser JS heap memory ($< 60\text{ MB}$).
- [x] **8.3 Pre-Dashboard Login Portal**:
  - [x] Cyber-tactical authentication screen with enclave status pills and show/hide password toggle.
- [x] **8.4 Top HUD Metric Banner**:
  - [x] 5 tactical cards (Total Up, Down/Outage, Suppressed, High CPU >80%, High RAM >80%).
- [x] **8.5 Logical Topology Tree (Tab 1)**:
  - [x] Interactive SVG/Canvas tree: Horizontal backbone (Core Router $\rightarrow$ Distribution/Aggregate $\rightarrow$ Access Switches) + **vertical 2-branch parallel endpoint columns** under Access Switches.
  - [x] Smooth zoom and pan controls.
  - [x] Real-time node status badges with compiler-hoisted static cables.
  - [x] Node click opens slide-over inspector.
- [x] **8.6 Slide-Over Telemetry Inspector**:
  - [x] Decoupled network ICMP/NTP telemetry and host health metrics.
  - [x] Real-time gauges for CPU %, RAM %, and normalized multi-volume disk storage partitions (`C:`, `D:`).
  - [x] Historical latency sparkline chart.
  - [x] Maintenance mode toggle directly from inspector.
- [x] **8.7 Tabular Registries with Tactical Pagination**:
  - [x] Standardized header "+", inline edit/delete actions across Inventory, Groups, Discovery, Ingestion, and Audit tables.
  - [x] Standardized pagination footer (`[ 25 | 50 | 100 | 250 ]`, range counters, page pills).
- [x] **8.8 Interactive Staging Grid (Bulk Ingestion)**:
  - [x] Spreadsheet-like grid with inline cell editing for incomplete/conflicting rows.
  - [x] Batch commit modal with `commit_valid_only: true` partial commit toggle.
- [x] **8.9 1:1 Schema Modal Dialogs**:
  - [x] Create/Edit Device modal reflecting all schema fields with locked MAC validation.
  - [x] Orphan-Safe Deletion Dialog (`modal-delete-confirm`).
  - [x] Password update modal with complexity validation and visibility toggles.
- [x] **8.10 Automated Test Suite & Fullstack Serving Verification**:
  - [x] `vue-tsc` completed with zero TypeScript errors.
  - [x] `vite build` compiled all 1585 modules into `/frontend/dist` in 10.49s.
  - [x] Kestrel server successfully serves static SPA `index.html` at root and `/assets/*` with HTTP 200.
  - [x] All 147 backend unit & integration tests passing. Phase 8 complete.

---

### Phase 9: Fullstack Integration, Unified Serving & Hardening
**Status:** `100% COMPLETE & VERIFIED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 9](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-9-fullstack-integration-unified-serving--hardening)

- [x] **9.1 Unified Self-Contained Static Serving**:
  - [x] Configured MSBuild publish target bundling `/frontend/dist` into `wwwroot` automatically upon release compilation.
  - [x] Hardened ASP.NET Core `ContentRootPath` defaulting to `AppContext.BaseDirectory` so executable operates seamlessly from any working directory or service runner.
  - [x] Serves compiled Vue 3 SPA on root paths (`/`, `/assets/*`) and REST API on `/api/v1/...` under a single port with zero CORS.
- [x] **9.2 Memory Benchmarking & Bounded Footprint Verification**:
  - [x] Live process working set verified at ~265 MB, well within the strict 500 MB RAM budget.
  - [x] Bulk upload streaming engine uses 64KB chunk buffer and periodic `ChangeTracker.Clear()` to bound RSS memory $< 15\text{MB}$.
  - [x] Frontend Pinia stores enforce `shallowRef()` to bound browser JS heap $< 60\text{MB}$.
- [x] **9.3 Strict Air-Gap Compliance**:
  - [x] Zero external network requests or remote CDN dependencies (no Google Fonts, no external CDNs, 100% bundled locally).
  - [x] Constant-time Argon2id password verification ($m=64\text{MB}, t=3, p=4$) with side-channel mitigation for deactivated accounts.
  - [x] Native async ICMP socket pinging with zero subprocess spawning.
- [x] **9.4 Production Build & Automated Verification**:
  - [x] Packaged release binary via `dotnet publish -c Release -o dist/server`.
  - [x] Executed live end-to-end verification of `dist/server/NetworkObserver.Api.exe` on port 5005: health UP, static SPA HTML and JS bundles served with HTTP 200, authentication token issuance, audit log query, and process working set benchmark verified.
  - [x] All 147 backend unit & integration tests passing. Phase 9 complete.

---

### Phase 10: [DEFERRED: FUTURE DEVELOPMENT / MILESTONE 2] Physical Floorplan Canvas
**Status:** `DEFERRED TO MILESTONE 2`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 10](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-10-deferred-future-development--milestone-2-physical-floorplan-canvas)

- [ ] [DEFERRED] Architectural CAD floorplan rendering with spatial coordinate beacons.
