# Network Observer — Software Architecture & Supporting Libraries Specification
**Document Version:** 1.0  
**Project:** Network Observer (Industrial LAN Telemetry & Topology Management Platform)  
**Classification:** Restricted — Air-Gapped Industrial Enclave (SCADA / NOC Deployment)  
**Location:** `d:\FullStackProjects\Network monitor`  
**Last Updated:** 18 October 2026  
**Status:** APPROVED ARCHITECTURAL BLUEPRINT  

---

## 1. Executive Architectural Overview

Network Observer is engineered as a **Fullstack Unified Monolith** operating in an air-gapped industrial environment. The system monitors up to 1,000 LAN assets with continuous ICMP telemetry (5-second polling tick) and host health interrogation (30-second polling tick).

To satisfy the core non-negotiable requirements of **exceptional code readability, zero raw SQL queries, clean cognitive flow, strict SOLID design patterns, and a controlled 500 MB total RAM memory allowance**, the architecture transitions to a **Strict 4-Tier Layered Architecture** on the backend and a **Layered Feature-Driven Architecture** on the frontend.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        FULLSTACK UNIFIED MONOLITH                            │
│                     (Listening on HTTPS Port 8443)                           │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │             PRESENTATION LAYER (ASP.NET Core Web API)                  │  │
│  │  - REST Controllers (/api/v1/...)   - Request Validation (FluentValidation)│
│  │  - RBAC & Session Middleware        - Static SPA Hosting (/dist)       │  │
│  └───────────────────────────────────┬────────────────────────────────────┘  │
│                                      │ (DTOs)                                │
│                                      ▼                                       │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │              APPLICATION LAYER (Use Cases & Business Rules)            │  │
│  │  - Business Services (DeviceService, TopologyService, SuppressionEngine)│
│  │  - Interfaces (IDeviceService, IPingEngine, IAuditLogger)              │  │
│  │  - Business Rules (Cascading Alert Suppression, Maintenance Toggles)   │  │
│  └───────────────────┬──────────────────────────────────┬─────────────────┘  │
│                      │                                  │                    │
│                      ▼                                  ▼                    │
│  ┌─────────────────────────────────────┐  ┌───────────────────────────────┐  │
│  │       DOMAIN LAYER (Entities)       │  │   INFRASTRUCTURE LAYER        │  │
│  │  - Pure Domain Entities (Device,    │  │  - EF Core DbContext & LINQ   │  │
│  │    Group, Site, User, AuditLog)     │  │  - Npgsql PostgreSQL Driver   │  │
│  │  - Value Objects & Enums            │  │  - BackgroundService Daemons  │  │
│  │  - Domain Exceptions                │  │    (IcmpPoller, HealthPoller) │  │
│  │  (Zero Framework Dependencies)      │  │  - System.Net.NetworkInfo Ping│  │
│  └─────────────────────────────────────┘  └──────────────┬────────────────┘  │
└──────────────────────────────────────────────────────────┼───────────────────┘
                                                           │ TCP / Port 5432
                                                           ▼
                                            ┌──────────────────────────────┐
                                            │   PostgreSQL 18 Database     │
                                            │  (Partitioned Tables,        │
                                            │   Immutability Triggers)     │
                                            └──────────────────────────────┘
```

---

## 2. Definitive Library & Dependency Catalog

All libraries have been evaluated against performance, memory footprint ($\le 500\text{ MB}$ total system allowance), zero raw SQL requirement, clean maintainability, and suitability for air-gapped SCADA/NOC deployment.

### 2.1 Backend Technology Stack (C# / .NET 8)

| Component / Responsibility | Library / Package | Version | Purpose & Architectural Rationale |
| :--- | :--- | :---: | :--- |
| **Runtime & Framework** | `Microsoft.NET.Sdk.Web` (.NET 8 LTS) | `8.0.x` | High-performance multithreaded runtime, Kestrel web server, native asynchronous Task scheduler. |
| **Object-Relational Mapping (ORM)** | `Microsoft.EntityFrameworkCore` | `8.0.x` | Industry gold standard ORM. Eliminates raw SQL via 100% type-safe compile-time LINQ expressions. |
| **PostgreSQL Provider** | `Npgsql.EntityFrameworkCore.PostgreSQL` | `8.0.x` | High-throughput PostgreSQL driver supporting range partitioning, connection pooling, and binary streaming. |
| **Database Migrations** | `Microsoft.EntityFrameworkCore.Design` | `8.0.x` | Automated code-first migration generation (`dotnet ef migrations add`). |
| **Password Cryptography** | `Isopoh.Cryptography.Argon2` | `2.0.x` | Pure C# / native SIMD-accelerated Argon2id implementation ($m=64\text{MB}, t=3, p=4$) matching Security Spec § 3. |
| **CSPRNG Session Generation** | `System.Security.Cryptography.RandomNumberGenerator` | Built-in | Cryptographically secure 256-bit entropy token generation for operator sessions. |
| **ICMP Telemetry Engine** | `System.Net.NetworkInformation.Ping` | Built-in | Asynchronous batch pinging (`SendPingAsync`) with zero external native DLL dependencies. |
| **Background Scheduling** | `Microsoft.Extensions.Hosting.BackgroundService` | Built-in | Native asynchronous daemon framework with `PeriodicTimer` for deterministic 5s / 30s polling ticks. |
| **Input Validation** | `FluentValidation.AspNetCore` | `11.x` | Strongly typed validation rules for incoming REST DTOs, keeping controllers completely clean. |
| **Structured Logging** | `Serilog.AspNetCore` + `Serilog.Sinks.File` | `8.x` | High-performance non-blocking structured JSON file logging with daily rotation. |
| **API Documentation** | `Swashbuckle.AspNetCore` (Swagger) | `6.5.x` | OpenAPI contract generation matching `api_contracts_specification.md`. |

### 2.2 Frontend Technology Stack (Vue 3 + Vite)

| Component / Responsibility | Library / Package | Version | Purpose & Architectural Rationale |
| :--- | :--- | :---: | :--- |
| **SPA Framework** | `vue` (Vue 3 Composition API with `<script setup>`) | `^3.4.x` | Fine-grained proxy reactivity, fast DOM updates without Virtual DOM waterfall recalculations. |
| **Build Tooling** | `vite` | `^5.2.x` | Instant HMR development and optimized production bundling into `/dist`. |
| **Language** | `typescript` | `^5.4.x` | End-to-end type safety matching backend C# DTO contracts. |
| **Topology Visualization** | `cytoscape` + `cytoscape-dagre` | `^3.29.x` | Multi-layer HTML5 Canvas engine. Handles 1,000+ nodes at 60fps pan/zoom. Built-in graph theory (`.successors()`) for cascading alert suppression. Declarative CSS-like cyber styling. |
| **Telemetry & HUD Charts** | `echarts` + `vue-echarts` | `^5.5.x` | Canvas-based tactical dark charts, glowing neon gradients, gauges, and latency sparklines ($\approx 35\text{ MB}$ RAM). |
| **State Management** | `pinia` | `^2.1.x` | Modular, type-safe global stores (`auth`, `topology`, `telemetry`, `inventory`). |
| **Styling & Design System** | `tailwindcss` + `@tailwindcss/forms` | `^3.4.x` | Cyber-tactical dark palette (`#050811`, `#070b16`, neon cyan, warning amber, alert crimson). |
| **Tactical Iconography** | `lucide-vue-next` | `^0.360.x` | Clean, crisp, lightweight SVG icons for network hardware, racks, and alert indicators. |
| **HTTP Transport** | `axios` | `^1.6.x` | Interceptors for session cookie credentials, 401 redirect guards, and standardized error parsing. |

---

## 3. Strict 4-Tier Backend Layered Architecture (C# .NET 8)

The backend is organized into 4 distinct projects (or namespaces) with **strictly enforced unidirectional dependency rules**:

```
Presentation Tier (NetworkObserver.Api)
       │
       ▼
Application Tier (NetworkObserver.Application)
       │
       ├────────────────────────────────────────┐
       ▼                                        ▼
Domain Tier (NetworkObserver.Domain)   Infrastructure Tier (NetworkObserver.Infrastructure)
```

### 3.1 Tier 1: Presentation Layer (`NetworkObserver.Api`)
* **Responsibilities**:
  1. Expose REST endpoints conforming strictly to `api_contracts_specification.md`.
  2. Perform request authentication via `SessionAuthMiddleware` (extracting `observer_session` cookie).
  3. Enforce Role-Based Access Control via `[AuthorizeRole(RoleType.NetworkAdmin)]`.
  4. Validate incoming payload syntax via `FluentValidation`.
  5. Host and serve compiled Vue 3 `/dist` assets on root fallback routes.
* **Inviolable Boundary Rule**:
  * Controllers **NEVER** inject `NetworkObserverDbContext`.
  * Controllers **NEVER** write LINQ queries.
  * Controllers only inject Application Service interfaces (`IDeviceService`, `ITopologyService`) and return standardized `ApiResponse<T>`.

### 3.2 Tier 2: Application Layer (`NetworkObserver.Application`)
* **Responsibilities**:
  1. Implement all business use cases and operational workflows.
  2. **Cascading Failure & Alert Suppression Engine**: When a parent switch transitions to `OFFLINE`, automatically compute all downstream devices via topology adjacency and mark them `SUPPRESSED` (`status_id = 3`, `is_suppressed = true`), halting synthetic alerts.
  3. **Natural Key Immutability Enforcement**: Enforce that `mac_address` cannot be modified after initial provisioning (rejects with `MacAddressImmutableException`).
  4. **Device Maintenance Mode**: Handle `PUT /api/v1/devices/{id}/maintenance`, turning nodes to `UNKNOWN` (`status_id = 4`) and logging audit events.
  5. Orchestrate DTO-to-Entity conversions and validate business rules.

### 3.3 Tier 3: Domain Layer (`NetworkObserver.Domain`)
* **Responsibilities**:
  1. Core enterprise entities: `Device`, `Group`, `Site`, `User`, `Role`, `AuditLog`, `ReachabilitySample`, `HealthSample`, `MonitoringConfig`.
  2. Domain enums: `DeviceStatus` (`ONLINE=1`, `OFFLINE=2`, `SUPPRESSED=3`, `UNKNOWN=4`), `DeviceType`, `RoleType`, `AuditActionCode`.
  3. Value objects: `IpAddress`, `MacAddress`.
  4. Domain exceptions: `DeviceNotFoundException`, `MacAddressImmutableException`, `UnauthorizedRoleException`.
* **Inviolable Boundary Rule**:
  * Contains **zero external dependencies**. No references to ASP.NET Core, EF Core, or database drivers. Pure C# standard types.

### 3.4 Tier 4: Infrastructure Layer (`NetworkObserver.Infrastructure`)
* **Responsibilities**:
  1. **Persistence**: `NetworkObserverDbContext`, entity Fluent API configurations, and EF Core migrations.
  2. **Background Daemon 1 (`IcmpPollerWorker`)**: Inherits `BackgroundService`. Uses `PeriodicTimer(TimeSpan.FromSeconds(5))` to execute asynchronous batch pinging via `System.Net.NetworkInformation.Ping` across all `is_monitored = true` devices.
  3. **Background Daemon 2 (`HealthPollerWorker`)**: Inherits `BackgroundService`. Uses `PeriodicTimer(TimeSpan.FromSeconds(30))` for host telemetry collection.
  4. **Security & Cryptography**: Implements `IPasswordHasher` via Argon2id and `ISessionTokenGenerator` via `RandomNumberGenerator`.

---

## 4. Layered Feature-Driven Frontend Architecture (Vue 3)

The frontend mirrors the backend separation of concerns to prevent component bloat and maintain strict memory bounds.

```
frontend/src/
├── api/                           <-- Transport Layer (HTTP / Axios)
│   ├── apiClient.ts               (Axios instance, withCredentials: true, error interceptor)
│   ├── authApi.ts                 (POST /auth/login, POST /auth/logout, GET /auth/me)
│   ├── devicesApi.ts              (GET /devices, POST /devices, PUT /devices/{id})
│   ├── topologyApi.ts             (GET /topology/tree, GET /monitoring/config)
│   └── auditApi.ts                (GET /audit/logs)
│
├── types/                         <-- Domain Types & DTO Contracts (Matches C# DTOs)
│   ├── device.ts                  (DeviceDto, DeviceStatus, DeviceType, CreateDeviceRequest)
│   ├── topology.ts                (CytoscapeNode, EdgeDefinition, TopologyTreeDto)
│   └── telemetry.ts               (ReachabilitySampleDto, HealthSampleDto)
│
├── stores/                        <-- Application State Layer (Pinia)
│   ├── useAuthStore.ts            (Session user, role, shift expiry timer)
│   ├── useTopologyStore.ts        (Graph nodes/edges, selected node, Cytoscape instance via markRaw)
│   ├── useTelemetryStore.ts       (High-frequency polling buffer managed via shallowRef)
│   └── useInventoryStore.ts       (Paginated device registry, active filter criteria)
│
├── features/ (or views/)          <-- Presentation Views & Workflows
│   ├── topology/                  (TopologyCanvas.vue, CytoscapeWrapper.vue, LayoutControls.vue)
│   ├── inventory/                 (DeviceRegistryTable.vue, DeviceDrawer.vue, BulkImportModal.vue)
│   ├── telemetry/                 (TopHudCards.vue, EChartsLatencySparkline.vue)
│   ├── audit/                     (AuditLogViewer.vue, AuditFilterBar.vue)
│   └── settings/                  (MonitoringConfigForm.vue, UserAdminTable.vue)
│
└── components/common/             <-- Tactical Cyber Design System Primitives
    ├── TacticalButton.vue         (Glow borders, tactical sound triggers, loading state)
    ├── TacticalCard.vue           (Dark `#070b16` panel, cyan header accents)
    ├── StatusLed.vue              (Green/red/amber/slate pulsating status indicator)
    ├── TacticalTablePagination.vue(Standardized footer: [ 25 | 50 | 100 | 250 ])
    └── ModalDialog.vue            (Keyboard ESC trap, backdrop blur)
```

---

## 5. Inviolable Reactivity & Memory Guardrails

To comfortably operate within the **500 MB total RAM memory allowance** while maintaining high rendering performance and zero UI stutter:

### Rule 1: `markRaw()` on Visual Graph & Chart Instances
Never allow Vue's reactive proxy engine to wrap third-party canvas instances:
```typescript
// INVIOLABLE: Cytoscape & ECharts instances MUST be marked raw
import { markRaw } from 'vue';
import cytoscape from 'cytoscape';

const cyInstance = markRaw(cytoscape({
  container: canvasContainer.value,
  elements: graphElements.value,
  style: tacticalStylesheet
}));
```
*Rationale*: Wrapping Cytoscape's internal node graph inside Vue 3's reactive proxy causes memory consumption to balloon by $300\%$ and causes severe frame-rate degradation.

### Rule 2: `shallowRef()` for High-Frequency Telemetry Arrays
When the 5-second polling tick delivers batch telemetry for 1,000 devices:
```typescript
// INVIOLABLE: Use shallowRef for high-frequency streaming arrays
import { shallowRef } from 'vue';

const liveTelemetrySamples = shallowRef<Record<number, ReachabilitySample>>({});

function onTelemetryTick(newBatch: Record<number, ReachabilitySample>) {
  // Triggers single component update on reference change without deep proxy traversals
  liveTelemetrySamples.value = newBatch;
}
```

### Rule 3: Single Canvas Batching during Telemetry Ticks
When updating node colors on polling ticks in Cytoscape:
```typescript
// Execute all node color updates in a single batch
cyInstance.batch(() => {
  for (const sample of newSamples) {
    const node = cyInstance.$(`#device-${sample.deviceId}`);
    node.data('status', sample.status);
  }
});
```

---

## 6. Monolith Deployment & Static File Hosting

In production, the compiled Vue 3 application (`/frontend/dist`) is embedded directly into the ASP.NET Core Kestrel pipeline:

```csharp
// Program.cs — Fullstack Monolith Configuration
var builder = WebApplication.CreateBuilder(args);

// Add 4-Tier services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Host compiled Vue 3 SPA
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html"); // Handles Vue Router HTML5 history mode

app.Run("https://0.0.0.0:8443");
```

A single command publishes a self-contained, high-performance binary:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 7. Architectural Compliance Matrix

| Requirement | Architectural Mechanism | Verified In Spec |
| :--- | :--- | :---: |
| **Zero Raw SQL Queries** | EF Core with compile-time LINQ queries & DbContext | Section 2.1, 3.4 |
| **High Readability & Flow** | Strict 4-Tier Layered Architecture with dedicated Application Services | Section 3 |
| **SOLID Principles & Clean Code** | Explicit SRP, OCP, LSP, ISP, and DIP enforcement across all tiers | Section 8.1 |
| **Design Patterns Catalog** | Factory, Strategy, Singleton, and Repository abstractions implemented | Section 8.2 |
| **500 MB Total RAM Budget** | Multi-tier memory allocation: Kestrel, Argon2id, Caches, Ring Buffers, Canvas | Section 5, 8.3 |
| **1,000-Node 60fps Topology** | Cytoscape.js multi-layer canvas with `cy.batch()` updates | Section 2.2, 5 |
| **Downstream Alert Suppression** | Domain Service utilizing Cytoscape graph adjacency / EF Core hierarchy | Section 3.2 |
| **Fullstack Monolith** | Single ASP.NET Core executable serving API + Vue 3 `/dist` on port 8443 | Section 6 |

---

## 8. Software Engineering Standards, SOLID Principles & Design Patterns Catalog

To ensure the codebase remains maintainable, modular, and enterprise-grade over its operational lifecycle in air-gapped infrastructure, the system mandates the following software engineering practices.

### 8.1 SOLID Principles Enforcement

#### 1. Single Responsibility Principle (SRP)
* Every class, service, and controller must have **only one reason to change**.
* *Application*:
  - `Argon2idPasswordHasher`: Solely responsible for password hashing and verification against RFC 9106 parameters.
  - `SessionManager`: Solely responsible for issuing, caching, validating, and revoking active operator session tokens.
  - `DeviceService`: Solely responsible for device domain operations and business rules (e.g. MAC address immutability).
  - `HealthController` / `AuthController`: Solely responsible for HTTP translation, route negotiation, and mapping domain responses to `ApiResponse<T>`.

#### 2. Open/Closed Principle (OCP)
* Software entities must be **open for extension, but closed for modification**.
* *Application*:
  - Telemetry collection is designed around a polling strategy interface (`ITelemetryPollerStrategy`). Adding future polling protocols (e.g., Modbus, OPC-UA, or WMI) requires creating a new strategy class rather than modifying the polling engine daemon.
  - Ingestion validators and export serializers implement strategy contracts (`IExportFormatterStrategy`), enabling new formats without touching core pipeline logic.

#### 3. Liskov Substitution Principle (LSP)
* Subtypes must be substitutable for their base types without altering system correctness.
* *Application*:
  - Any implementation of `IPasswordHasher`, `ISessionManager`, or `ITelemetryPoller` can replace standard implementations in unit tests (e.g., an in-memory test hasher or mock ping strategy) without unexpected side effects or broken preconditions.

#### 4. Interface Segregation Principle (ISP)
* Clients should not be forced to depend on interfaces they do not use.
* *Application*:
  - Interfaces are fine-grained and role-tailored (e.g., `ISessionReader` for middleware reading sessions vs `ISessionWriter` for login/logout operations; `ITopologyQueryService` for read-only tree retrieval vs `ITopologyCommandService` for link mutations).

#### 5. Dependency Inversion Principle (DIP)
* High-level business modules must not depend on low-level implementation details; both must depend on abstractions.
* *Application*:
  - The Presentation tier (`NetworkObserver.Api`) and Application tier (`NetworkObserver.Application`) have **zero references** to low-level persistence, database drivers, or socket APIs.
  - The Application tier defines interfaces (`IDatabaseHealthService`, `ISessionManager`, `IPasswordHasher`), and the Infrastructure tier (`NetworkObserver.Infrastructure`) provides concrete implementations injected via standard Microsoft Dependency Injection.

---

### 8.2 Design Patterns Catalog

```
┌────────────────────────────────────────────────────────────────────────┐
│                        GOF DESIGN PATTERNS                             │
├───────────────────┬────────────────────────────────────────────────────┤
│ Pattern           │ Concrete Implementation in Network Observer        │
├───────────────────┼────────────────────────────────────────────────────┤
│ 1. Strategy       │ - Telemetry Poller Strategies (ICMP, NTP, SNMP)    │
│                   │ - Export Serialization (CSV, JSON, SQLite)         │
│                   │ - Ingestion Validation Rules                       │
├───────────────────┼────────────────────────────────────────────────────┤
│ 2. Factory        │ - Cryptographic Token Factory (CSPRNG 256-bit)    │
│                   │ - Telemetry Probe Request Factory                  │
│                   │ - Entity / DTO Mapping Factories                   │
├───────────────────┼────────────────────────────────────────────────────┤
│ 3. Singleton      │ - In-Memory Session Cache (`ISessionManager`)       │
│                   │ - Global Polling Configuration Cache               │
│                   │ - In-Memory Topology Adjacency Graph Cache         │
├───────────────────┼────────────────────────────────────────────────────┤
│ 4. Repository     │ - Entity Data Access Abstractions (`IDeviceRepo`)  │
│    / Query Spec   │ - Encapsulates EF Core LINQ queries from Services  │
└───────────────────┴────────────────────────────────────────────────────┘
```

#### 1. Strategy Pattern
* **Telemetry Polling Engine**:
  ```csharp
  public interface ITelemetryPollerStrategy
  {
      string ProtocolName { get; }
      Task<TelemetryResult> ProbeAsync(Device device, CancellationToken ct);
  }
  ```
  Concrete strategies: `IcmpPingPollerStrategy`, `NtpDriftPollerStrategy`, `HttpHostAgentPollerStrategy`.
* **Export Serialization Engine**:
  ```csharp
  public interface IExportStrategy
  {
      string Format { get; }
      Task<byte[]> ExportAsync(IEnumerable<Device> devices, CancellationToken ct);
  }
  ```
  Concrete strategies: `CsvExportStrategy`, `JsonExportStrategy`, `SqliteExportStrategy`.

#### 2. Factory Pattern
* **Session Token Factory**:
  ```csharp
  public interface ISessionTokenFactory
  {
      SessionToken CreateToken(int userId, short roleId);
  }
  ```
  Encapsulates 256-bit CSPRNG entropy extraction, prefix formatting (`obs_tok_...`), and cryptographically secure initialization.

#### 3. Singleton Pattern
* **Thread-Safe Session Cache (`ISessionManager`)**:
  Registered as a Singleton service in DI. Uses thread-safe collections (`ConcurrentDictionary<string, SessionProfile>`) to validate incoming bearer tokens in sub-millisecond time without incurring database roundtrips.
* **Topology Adjacency Graph**:
  In-memory graph cache updated atomically on device/group mutations, providing $O(1)$ fast lookups for cascading alert suppression during network partitions.

#### 4. Repository & Query Specification Pattern
* Business services do not query `DbContext` directly; queries are abstracted behind clean Repository interfaces (e.g., `IDeviceRepository`, `IAuditLogRepository`), enabling unit testing, query reuse, and explicit separation of business logic from persistence mechanics.

---

### 8.3 500 MB Total RAM Budget Allocation Table

With the relaxed **500 MB total RAM ceiling**, the system allocates memory across subsystems with generous headroom while strictly guarding against unconstrained leaks during long-running 8-hour operator shifts:

| Subsystem / Component | Allocation Ceiling | Architectural Mechanism & Guardrails |
| :--- | :---: | :--- |
| **.NET Runtime & Kestrel Web Server** | $\approx 120\text{ MB}$ | Native thread pool, Kestrel HTTP pipeline, ASP.NET Core DI, Server GC with Gen2 compaction. |
| **Argon2id Cryptographic Scratchpad** | $\approx 64\text{ MB}$ | High-security parameter $m=64\text{MB}$. Allocated strictly during active authentication attempts and immediately reclaimed. |
| **In-Memory Caches (Singleton Services)** | $\approx 35\text{ MB}$ | ConcurrentDictionary session cache (max 250 active operators), topology adjacency graph, monitoring config singleton. |
| **Telemetry Ingestion Ring Buffers** | $\approx 45\text{ MB}$ | Fixed-size ring buffers for 1-second micro-batching into PostgreSQL partitioned tables. |
| **Frontend SPA JS Heap (Vue 3 Client)** | $\approx 110\text{ MB}$ | Cytoscape.js HTML5 canvas with `markRaw()`, Apache ECharts with `shallowRef()`, Pinia state stores. |
| **Safety Headroom & Buffer** | $\approx 126\text{ MB}$ | Dynamic OS paging, bulk CSV parsing spikes, GC headroom before high-frequency collections. |
| **TOTAL ALLOWABLE MEMORY** | **$\le 500\text{ MB}$** | **Guaranteed by architectural limits, fixed buffers, and memory pooling.** |
