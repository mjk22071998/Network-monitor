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
| **Current Milestone** | **Milestone 1: Architectural Baseline & Scaffolding** | Specifications complete; ready for code scaffolding. |
| **Active Phase** | **Phase 0: Workspace Scaffolding & Master Build Setup** | Ready to scaffold .NET 8 4-Tier solution & Vue 3. |
| **Specifications Status** | **100% COMPLETE & LOCKED** | All 7 master specification documents approved. |
| **Database Engine** | **PostgreSQL 16/17/18** | 3NF DDL, Range Partitioning, PL/pgSQL Triggers locked. |
| **Backend Stack** | **C# (.NET 8) Web API + EF Core** | 4-Tier Layered Architecture, Npgsql, Async Ping, Zero Raw SQL. |
| **Frontend Stack** | **Vue 3 + Vite + Cytoscape.js + ECharts** | Pinia, fine-grained proxy reactivity, Cyber-Tactical theme. |
| **Memory Architecture**| **60 MB – 100 MB Working Ceiling** | `markRaw()` on canvas, `shallowRef()` on telemetry batches. |
| **Overall Completion** | **18% (Architecture Blueprint & Step 0.1 Locked)**| Specification suite harmonized; Git repo initialized. |

## 2. Mandatory Verification Protocol

> [!IMPORTANT]
> **Strict Step-by-Step Verification Mandate**  
> Every task in this checklist requires **verifiable proof** before being marked as completed (`[x]`). We will execute verification commands (terminal outputs, build logs, HTTP curl tests, browser checks) and review results together before advancing to the next step. No steps will be grouped or skipped without explicit demonstration and approval.

---

## 3. Phase-by-Phase Execution Checklist

### Phase 0: Workspace Scaffolding & Master Build Setup
**Status:** `READY TO EXECUTE`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 0](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-0-workspace-scaffolding--master-build-pipeline)  
**Architecture Reference:** [`software_architecture_and_libraries_specification.md`](file:///d:/FullStackProjects/Network%20monitor/software_architecture_and_libraries_specification.md)

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
- [ ] **0.3 Minimal ASP.NET Core API Compilation & Health Check**:
  - [ ] Install core NuGet packages (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`).
  - [ ] Configure minimal health check endpoint `GET /api/v1/health` returning `{"status": "UP", "version": "2.2"}`.
  - [ ] Compile solution via `dotnet build`.
  - *Verification Proof*: Run `dotnet run --project backend/src/NetworkObserver.Api` and execute `curl.exe http://localhost:5000/api/v1/health` verifying `200 OK` JSON response.
- [ ] **0.4 Frontend Project Initialization (Vue 3 + Vite + Tailwind + Cytoscape + ECharts)**:
  - [ ] Initialize Vue 3 TypeScript project in `/frontend` via `npm.cmd`.
  - [ ] Install Tailwind CSS, `@tailwindcss/forms`, Lucide icons, Pinia, Cytoscape.js, and Apache ECharts.
  - [ ] Configure `vite.config.ts` with API proxy (`/api` $\rightarrow$ `http://localhost:5000`).
  - *Verification Proof*: Run `npm.cmd run build` (successful compilation into `/frontend/dist`) and test proxy connection to .NET backend.

---

### Phase 1: PostgreSQL 18 Database Engine & 3NF Schema Initialization
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 1](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-1-postgresql-18-database-engine--3nf-schema-initialization)  
**Schema Reference:** [`database_schema_and_ingestion_specification.md` § 4](file:///d:/FullStackProjects/Network%20monitor/database_schema_and_ingestion_specification.md#L111)

- [ ] **1.1 Database Migration Scripts**:
  - [ ] Write `001_initial_schema.sql` with 3NF DDL for all core tables.
  - [ ] Implement declarative range partitioning for `reachability_samples` by `recorded_at`.
  - [ ] Implement declarative range partitioning for `health_samples` by `recorded_at`.
  - [ ] Create default partitions (`reachability_samples_default`, `health_samples_default`).
- [ ] **1.2 Non-Repudiation Triggers**:
  - [ ] Author `fn_audit_logs_immutable()` PL/pgSQL function.
  - [ ] Attach `trg_audit_logs_no_update` and `trg_audit_logs_no_delete` triggers to `audit_logs`.
- [ ] **1.3 Master Seed Data**:
  - [ ] Pre-seed Site 1 (`Primary Site Enclave`).
  - [ ] Pre-seed Group 1 (`Unassigned Group`).
  - [ ] Pre-seed Roles (1: `NETWORK_ADMIN`, 2: `NETWORK_ENGINEER`, 3: `IT_SUPPORT`).
  - [ ] Pre-seed Device Types (1: `CORE_ROUTER`, 2: `AGGREGATE_SWITCH`, 3: `ACCESS_SWITCH`, 4: `SERVER`, 5: `PC`).
  - [ ] Pre-seed Monitoring Config singleton row 1.
  - [ ] Pre-seed initial `admin` account with Argon2id hash.
- [ ] **1.4 C++ Database Connection Pool (`libpqxx`)**:
  - [ ] Implement thread-safe connection pool class `DatabasePool`.
  - [ ] Implement automated startup migration verification routine.

---

### Phase 2: Security, Argon2id & Session Authentication Engine
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 2](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-2-security-argon2id--session-authentication-engine)  
**Security Reference:** [`security_and_rbac_specification.md`](file:///d:/FullStackProjects/Network%20monitor/security_and_rbac_specification.md)

- [ ] **2.1 Argon2id Password Cryptography**:
  - [ ] Implement Argon2id hashing wrapper with parameters: $m=64\text{MB}, t=3, p=4$.
  - [ ] Implement constant-time password verification (`CRYPTO_MEMCMP`).
  - [ ] Implement dummy hashing pass on deactivated accounts (`is_active = false`).
- [ ] **2.2 Cryptographic Session Manager**:
  - [ ] Implement 256-bit CSPRNG token generator (`BCryptGenRandom`).
  - [ ] Implement thread-safe in-memory session cache with `std::shared_mutex`.
  - [ ] Enforce 8-hour shift lifetime and 30-minute idle inactivity timeout.
  - [ ] Implement session revocation blacklist.
- [ ] **2.3 Crow RBAC Guard Middleware**:
  - [ ] Intercept requests; extract session from `observer_session` cookie or `Authorization: Bearer`.
  - [ ] Enforce route permissions per Master RBAC Matrix (`401` vs `403`).
- [ ] **2.4 Authentication API Endpoints**:
  - [ ] `POST /api/v1/auth/login`: Authenticate credentials, set Secure HttpOnly cookie, return numeric IDs.
  - [ ] `POST /api/v1/auth/logout`: Revoke active session (`204 No Content`).
  - [ ] `GET /api/v1/auth/me`: Return caller identity and role profile.
  - [ ] `PUT /api/v1/auth/password`: Authenticated self-service password update.
- [ ] **2.5 User Administration API (Admin Only)**:
  - [ ] `GET /api/v1/users`: List users with string `role` and `site_name`.
  - [ ] `POST /api/v1/users`: Pre-provision operator accounts (Zero self-registration).
  - [ ] `GET /api/v1/users/{id}` & `PUT /api/v1/users/{id}`: Profile updates & admin password reset.
  - [ ] `DELETE /api/v1/users/{id}`: Safe deactivation; prevent self-deletion (`409`).

---

### Phase 3: Core Enclave Entities & Device Inventory CRUD
**Status:** `NOT STARTED`  
**Roadmap Reference:** [`project_implementation_roadmap_and_phases.md` § Phase 3](file:///d:/FullStackProjects/Network%20monitor/project_implementation_roadmap_and_phases.md#phase-3-core-enclave-entities--device-inventory-crud)  
**API Reference:** [`api_contracts_specification.md` § 4, 5, 7](file:///d:/FullStackProjects/Network%20monitor/api_contracts_specification.md)

- [ ] **3.1 Enclave Site API**:
  - [ ] `GET /api/v1/site`: Return single site enclave data (`site_id = 1`).
  - [ ] `PUT /api/v1/site`: Admin update of site metadata.
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
