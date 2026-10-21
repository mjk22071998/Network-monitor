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
| **Current Milestone** | **Milestone 1: Architectural Baseline & Scaffolding** | Specifications complete; Phase 0, 1 & 2 verified. |
| **Active Phase** | **Phase 3: Core Enclave Entities & Device Inventory CRUD** | Phase 2 Security, Argon2id & Users API verified. |
| **Specifications Status** | **100% COMPLETE & LOCKED** | All master specification documents approved. |
| **Database Engine** | **PostgreSQL 18** | 3NF DDL, Range Partitioning, PL/pgSQL Triggers verified. |
| **Backend Stack** | **C# (.NET 10) Web API + EF Core 10** | 4-Tier Layered Architecture, Npgsql, Async Ping, Zero Raw SQL, SOLID & GoF Patterns. |
| **Frontend Stack** | **Vue 3 + Vite + Cytoscape.js + ECharts** | Pinia, fine-grained proxy reactivity, Cyber-Tactical theme. |
| **Memory Architecture**| **500 MB Total System RAM Allowance** | Explicit memory budget, `markRaw()` on canvas, `shallowRef()` on telemetry batches. |
| **Overall Completion** | **45% (Phase 0, 1 & 2 Complete & Verified)** | Ready for Phase 3 Core Enclave Entities & Device Inventory. |

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
**Status:** `IN PROGRESS`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 3](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-3-core-enclave-entities--device-inventory-crud)  
**API Reference:** [`api_contracts_specification.md` § 4, 5, 7](file:///d:/FullStackProjects/Network%20monitor/api_contracts_specification.md)

- [x] **3.1 Enclave Site API**:
  - [x] `GET /api/v1/site`: Return single site enclave data (`site_id = 1`) for all authenticated roles.
  - [x] `PUT /api/v1/site`: Admin update of site metadata with strict boundary validation (name, code, desc).
  - *Verification Proof*: Authored `SiteServiceTests.cs` (10 unit tests). Entire test suite expanded to 48 tests passing (`Total: 48, Passed: 48, Failed: 0`). Executed live verification against Kestrel server: admin authenticated `GET /api/v1/site` returned initial enclave (`SITE-01`), admin `PUT /api/v1/site` updated metadata (`Reactor Unit 4 Control Facility`, `FAC-R4-SCADA`), unauthenticated requests were rejected with `401 Unauthorized`, and engineer role `PUT` requests were rejected with `403 Forbidden`. Persistence in PostgreSQL 18 verified.
- [ ] **3.2 Nested Group Hierarchy API**:
  - [ ] `GET /api/v1/groups`: Nested recursive tree with **`children: [...]`** attribute.
  - [ ] `POST /api/v1/groups`: Create new group/subgroup.
  - [ ] `PUT /api/v1/groups/{id}`: Rename/move group; protect root Group 1.
  - [ ] `DELETE /api/v1/groups/{id}`: Fallback reassigning children to Group 1 (Unassigned).
- [ ] **3.3 Device Inventory CRUD API**:
  - [ ] `GET /api/v1/devices`: Paginated device records (10 schema fields).
  - [ ] `POST /api/v1/devices`: Create device; role-based switch vs endpoint checks.
  - [ ] `GET /api/v1/devices/{id}`: Single device profile.
  - [ ] `PUT /api/v1/devices/{id}`: Update device; reject MAC edit (`422 DEVICE_MAC_IMMUTABLE`).
  - [ ] `DELETE /api/v1/devices/{id}`: Safe delete setting children `parent_device_id = NULL`.
- [ ] **3.4 Global Polling Configuration API**:
  - [ ] `GET /api/v1/monitoring/config`: Return singleton configuration.
  - [ ] `PUT /api/v1/monitoring/config`: Admin update of polling cadences.

---

### Phase 4: High-Performance Multithreaded Telemetry Poller
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 4](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-4-high-performance-multithreaded-telemetry-poller)  
**SRS Reference:** [`master_software_requirements_specification.md` § 5.4–5.6](file:///d:/FullStackProjects/Network%20monitor/master_software_requirements_specification.md)

- [ ] **4.1 Native Raw Socket ICMP Engine**:
  - [ ] Implement Windows `IcmpSendEcho` polling loop (zero subprocess spawning).
  - [ ] Compute sub-millisecond round-trip time (`latency_us`) and packet loss.
  - [ ] Enforce Strict N/A rule: write `NULL` for missing/timed-out latency.
- [ ] **4.2 UDP NTP Clock Drift Engine**:
  - [ ] Implement raw UDP 48-byte NTP client on port 123.
  - [ ] Compute microsecond clock drift (`ntp_offset_us`); write `NULL` on timeout.
- [ ] **4.3 Cascading Failure Suppression Circuit**:
  - [ ] In-memory switch adjacency tree.
  - [ ] Automatically mark children `SUPPRESSED` on upstream switch failure.
  - [ ] Halt synthetic pings to suppressed nodes.
- [ ] **4.4 Host Health Metrics Collector**:
  - [ ] Poll host agents on port 9182 / SNMP on switches every 30 seconds.
  - [ ] Record CPU, RAM, network throughput, and multi-volume disk storage.
- [ ] **4.5 Micro-Batch Ingestion**:
  - [ ] Batch ICMP results into 1-second micro-transactions into partitioned tables.
- [ ] **4.6 Telemetry & Topology API Endpoints**:
  - [ ] `GET /api/v1/topology`: Return 2-branch logical tree and top HUD counters.
  - [ ] `GET /api/v1/telemetry/{id}`: Return live inspector metrics.
  - [ ] `GET /api/v1/telemetry/{id}/history`: Return time-series latency samples.

---

### Phase 5: Subnet Discovery Sweep & Operational Diagnostics
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 5](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-5-subnet-discovery-sweep--operational-diagnostics)

- [ ] **5.1 Asynchronous Subnet Discovery Worker**:
  - [ ] `POST /api/v1/discovery/scan`: Dispatch async sweep for CIDR (max `/24`).
  - [ ] Enforce max 1 concurrent scan per site (`409 Conflict`).
  - [ ] 10ms packet pacing between probes to prevent industrial PLC tripping.
  - [ ] Ping sweep, ARP cache read, SNMP bridge interrogation, MAC OUI typing heuristics.
- [ ] **5.2 Discovery Status API**:
  - [ ] `GET /api/v1/discovery/scan/{id}`: Return progress % and discovered assets.
- [ ] **5.3 Device Maintenance Mode Engine**:
  - [ ] `PUT /api/v1/devices/{id}/maintenance`: Toggle `is_monitored = false`.
  - [ ] Turn node slate-gray (`UNKNOWN`) and suppress cascade alerts.
  - [ ] Enforce role check: `IT_SUPPORT` restricted to leaf endpoints.
  - [ ] Log event to `audit_logs`.

---

### Phase 6: Low-Memory Streaming Bulk Ingestion Pipeline
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 6](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-6-low-memory-streaming-bulk-ingestion-pipeline)  
**Schema Reference:** [`database_schema_and_ingestion_specification.md` § 6](file:///d:/FullStackProjects/Network%20monitor/database_schema_and_ingestion_specification.md#L518)

- [ ] **6.1 Zero-RAM-Spike Streaming Upload**:
  - [ ] `POST /api/v1/ingest/upload`: Multipart upload handler with 64KB ring buffer.
  - [ ] Zero-copy row parsing via C++ `std::string_view`.
  - [ ] Direct streaming into `staged_devices` via `libpqxx::stream_to` (`COPY FROM STDIN`).
  - [ ] Verify RSS memory bound $< 15\text{ MB}$.
- [ ] **6.2 Automated Pre-Resolution**:
  - [ ] Resolve parent IP/MAC strings to `parent_device_id`.
  - [ ] Resolve nested group paths to `group_id`.
  - [ ] Flag rows: `VALID`, `INCOMPLETE`, `CONFLICT`.
- [ ] **6.3 Staging Batches & Keyset Grid API**:
  - [ ] `GET /api/v1/ingest/batches`: List persistent batches.
  - [ ] `GET /api/v1/ingest/batches/{id}/records`: Keyset paginated (`LIMIT 50`).
- [ ] **6.4 Inline Cell Editing**:
  - [ ] `PUT /api/v1/ingest/records/{id}`: In-place edit with instant re-validation.
- [ ] **6.5 Atomic Commit Gatekeeper (Admin Only)**:
  - [ ] `POST /api/v1/ingest/batches/{id}/commit`: Atomic insert into `devices`.
  - [ ] Support `commit_valid_only: true`.
  - [ ] Transactional rollback on error.
- [ ] **6.6 Batch Discard**:
  - [ ] `DELETE /api/v1/ingest/batches/{id}`: Purge staging records.

---

### Phase 7: Security Audit Trail Engine & Non-Repudiation
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 7](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-7-security-audit-trail-engine--non-repudiation)

- [ ] **7.1 Central Audit Logging Service**:
  - [ ] Implement `AuditLogger::log()` across all mutation routes.
  - [ ] Map all 20 action codes (`AUTH_LOGIN`, `DEVICE_CREATE`, etc.).
- [ ] **7.2 Trigger Tamper Verification**:
  - [ ] Verify `trg_audit_logs_no_update` and `trg_audit_logs_no_delete` abort tampering.
- [ ] **7.3 Immutable Audit Query API (Admin Only)**:
  - [ ] `GET /api/v1/audit/logs`: Paginated log query with ISO 8601 date range filters.

---

### Phase 8: Cyber-Tactical Vue 3 Frontend Construction
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 8](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-8-cyber-tactical-vue-3-frontend-construction)  
**UI Guidelines:** [`ui_guidelines_and_design_system.md`](file:///d:/FullStackProjects/Network%20monitor/ui_guidelines_and_design_system.md)

- [ ] **8.1 Cyber-Tactical Design System Setup**:
  - [ ] Configure Tailwind theme (`#050811`, `#070b16`, cyan accents, monospace typography).
  - [ ] Set up layout zones (Header, HUD, Main Canvas, Slide-Over, Tables).
- [ ] **8.2 Memory-Optimized Pinia Stores**:
  - [ ] Use `shallowRef()` for all tabular datasets and lists.
  - [ ] Ensure browser JS heap stays under 60MB.
- [ ] **8.3 Pre-Dashboard Login Portal**:
  - [ ] Cyber-tactical authentication screen with enclave status pills.
- [ ] **8.4 Top HUD Metric Banner**:
  - [ ] 5 cards (Up, Down, Suppressed, High CPU >80%, High RAM >80%).
- [ ] **8.5 Logical Topology Tree (Tab 1)**:
  - [ ] Interactive SVG tree: Horizontal backbone + **vertical 2-branch parallel endpoint columns** under Access Switches.
  - [ ] Smooth zoom and pan.
  - [ ] Real-time node status badges with compiler-hoisted static cables.
- [ ] **8.6 Slide-Over Telemetry Inspector**:
  - [ ] Live ICMP/NTP metrics, CPU/RAM gauges, multi-volume disk bars (`C:`, `D:`).
  - [ ] Latency time-series sparkline chart.
- [ ] **8.7 Tabular Registries with Tactical Pagination**:
  - [ ] Standardized header "+", inline edit/delete.
  - [ ] Standardized pagination footer (`[ 25 | 50 | 100 | 250 ]`, range counters, pills).
- [ ] **8.8 Interactive Staging Grid (Bulk Ingestion)**:
  - [ ] Spreadsheet-like grid with inline cell editing and commit modal.
- [ ] **8.9 1:1 Schema Modal Dialogs**:
  - [ ] Create/Edit Device modal with locked MAC validation.

---

### Phase 9: Fullstack Integration, Unified Serving & Hardening
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 9](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-9-fullstack-integration-unified-serving--hardening)

- [ ] **9.1 Unified Crow Static Serving**:
  - [ ] Crow serves compiled `/frontend/dist` on root paths.
  - [ ] Crow serves REST API on `/api/v1/...`.
  - [ ] Single executable, single port (`8443`), zero CORS.
- [ ] **9.2 Memory Benchmarking & Leak Verification**:
  - [ ] Ingest 10,000-row CSV; verify backend RSS stays under $15\text{ MB}$.
  - [ ] Run 2-hour continuous 5s polling test; verify Vue 3 heap stays under $60\text{ MB}$.
- [ ] **9.3 Air-Gap Verification**:
  - [ ] Verify zero external network requests or CDN calls.
  - [ ] Verify Argon2id constant-time authentication.
  - [ ] Verify native raw socket polling with zero subprocess spawning.

---

### Phase 10: [DEFERRED: FUTURE DEVELOPMENT / MILESTONE 2] Physical Floorplan Canvas
**Status:** `DEFERRED TO MILESTONE 2`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 10](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-10-deferred-future-development--milestone-2-physical-floorplan-canvas)

- [ ] [DEFERRED] Architectural CAD floorplan rendering with spatial coordinate beacons.
