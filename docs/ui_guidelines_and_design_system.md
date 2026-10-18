# User Interface Guidelines & Design System Specification
## System: Network Observer — Industrial LAN Telemetry & Topology Management Platform

**Document Version:** 1.0 (Cyber-Tactical SPA Specification)  
**Date:** 15 October 2026  
**Status:** Approved Master Baseline  
**Classification:** Technical / UI/UX Design System & Ergonomics  
**Specification Boundary:** Frontend Technology-Agnostic Design Contracts (Vue 3, React, Svelte, or Web Components).

---

> [!IMPORTANT]
> ### Authoritative Governance Directives & Operational Rules
> 1. **Documentation is the Absolute Single Source of Truth**:
>    This document, along with the PRD (`project_requirements_document.md`), Master SRS (`master_software_requirements_specification.md`), and Schema Specification (`database_schema_and_ingestion_specification.md`), constitutes the **sole, immutable authority** for all architecture, UI/UX structure, and system behavior. Every UI layout, component, modal form, route, and state behavior MUST strictly trace back to these documents.
>    * **Strict Drift Rejection Protocol**: If any change, new feature, visual divergence, or schema alteration is proposed or requested—even by the project owner/user—that is not tracked in or contradicts these specifications, the response MUST be:
>      > *"I cannot trace this back to the document / It contradicts the document."*
>    * **Documentation-First Rule**: The specification documents must be formally revised, harmonized, and approved **first** before any code, prototype, or styling implementation proceeds.
> 2. **Prototype Status & Implementation Boundary**:
>    The interactive prototype (`the_observer_tactical_spa.html`) is approved strictly as a **functional skeleton and structural item-placement reference**:
>    * **Approved Elements (Binding)**: Structural layout zones, top HUD metric cards, dual-canvas tabs (horizontal width-wise backbone + vertical 2-branch endpoints under access switches; physical layout from JSON without cables), right slide-over telemetry inspector, tabular registries with header right "+" create button, pagination controls, and 1:1 schema-aligned modal forms.
>    * **Visual UI/UX Overhaul Slated for Production**: The prototype does **not** represent final visual styling. The UI/UX aesthetics (color balancing, typography rendering, polish, padding, borders, animations, and micro-interactions) are preliminary/rough and will undergo an extensive redesign and visual overhaul during the frontend UI build phase.

---

## 1. Executive Summary & Design Vision

**Network Observer** delivers a mission-critical, military-grade **Cyber-Tactical Single Page Application (SPA)** engineered specifically for isolated, air-gapped industrial local area networks (SCADA, power plants, defense facilities, secure data centers).

### Core Aesthetic & Ergonomic Tenets
1. **Cyber-Tactical Command Aesthetic**: Deep obsidian and stealth-navy backgrounds (`#050811`, `#070b16`), razor-sharp technical borders, glowing neon telemetry beacons (emerald green, crimson red, amber warning, cyan/electric blue), and subtle radar gridlines.
2. **Ruthless Information Density**: Zero decorative whitespace bloat. Every pixel delivers actionable operational awareness. Technical identifiers, MAC addresses, IPv4/IPv6 addresses, latency microseconds, clock drift, and memory allocations are rendered in dedicated monospace typography (`JetBrains Mono`).
3. **Unified Single Page Application (SPA)**: Zero jarring full-page refreshes. Navigation flows seamlessly between the primary **Operations HUD (Dashboard & Dual Canvas)** and **Tabular Registries** (Devices, Sites, Groups/Subgroups, File Staging, Audit Trail).
4. **Context-Preserving Slide-Over Inspector**: Clicking any device across the topology tree or spatial floorplan opens a high-density, right-side telemetry panel without obscuring the canvas or navigating away from the active workflow.

---

## 2. Cyber-Tactical Design Tokens & Theme Specification

### 2.1 Color Palette

```
Surface Tokens:
  --bg-deep-void:       #050811   (Root application background)
  --bg-surface-nav:     #070b16   (Header, sidebar, modal backdrops)
  --bg-surface-card:    #090e1c   (Metric cards, node bodies, table containers)
  --bg-surface-elev:    #0d1527   (Hover states, input backgrounds, active tabs)
  
Border & Line Tokens:
  --border-subtle:      #151f33   (Grid lines, default card borders)
  --border-medium:      #1e293b   (Table cell dividers, panel separators)
  --border-cyan-glow:   #00f0ff   (Selected node halo, primary focus ring)
  
Status Glow & Telemetry Tokens:
  --status-reachable:   #10b981   (Emerald Green: Healthy, <2000ms RTT)
  --status-outage:      #f43f5e   (Crimson Red: Unreachable, Root Cause Switch Outage)
  --status-suppressed:  #f59e0b   (Tactical Amber: Downstream synthetic pings suppressed)
  --status-unknown:     #64748b   (Slate Gray: Newly provisioned / awaiting check)
  --status-cyan-active: #00f0ff   (Cyan Accent: Active selection, core router, healthy link)
```

### 2.2 Typography Hierarchy

| Role | Font Family | Size / Weight | Usage |
| :--- | :--- | :--- | :--- |
| **Display / Headers** | `Inter`, sans-serif | `text-base` (16px) / `font-bold` | Section headers, modal titles, site enclaves |
| **Labels & Body** | `Inter`, sans-serif | `text-xs` (12px) / `font-medium` | Field labels, tab text, navigation links |
| **Telemetry & Metrics** | `JetBrains Mono`, monospace | `text-2xl` (24px) / `font-bold` | Top HUD counters (Up, Down, CPU %, RAM %) |
| **Hardware Identifiers** | `JetBrains Mono`, monospace | `text-xs` (12px) / `font-semibold` | IP addresses (`192.168.1.1`), MACs (`00:1A:2B:AA:01`) |
| **Micro-Telemetry Badges**| `JetBrains Mono`, monospace | `text-[10px]` (10px) / `font-bold` | RTT latency (`1.2 ms`), NTP drift (`+0.4 µs`), ports |

---

## 3. Pre-Dashboard Authentication (Cyber-Tactical Login)

Before entering the operations dashboard, operators are authenticated via a dedicated, full-screen cyber-tactical login portal:

```
+-----------------------------------------------------------------------+
|  [RADAR GRID BACKGROUND]                                              |
|                                                                       |
|             +-------------------------------------------+             |
|             |  [CYBER SHIELD ICON]                     |             |
|             |  NETWORK OBSERVER                        |             |
|             |  Industrial LAN Telemetry Platform       |             |
|             |  [ENCLAVE: PRIMARY SITE | 100% AIR-GAP]  |             |
|             |-------------------------------------------|             |
|             |  OPERATOR CREDENTIALS                     |             |
|             |                                           |             |
|             |  Username:                                |             |
|             |  [ admin                                ] |             |
|             |                                           |             |
|             |  Passphrase:                              |             |
|             |  [ ********************                 ] |             |
|             |                                           |             |
|             |  [ AUTHENTICATE TO ENCLAVE (ENTER) ]      |             |
|             |-------------------------------------------|             |
|             |  Restricted Mission-Critical SCADA Enclave|             |
|             +-------------------------------------------+             |
|                                                                       |
+-----------------------------------------------------------------------+
```

### Functional Requirements for Login
1. **Authentication Handshake**: Dispatches credentials via `POST /api/v1/auth/login`. Returns a cryptographically secure session token and the assigned role (`NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`).
2. **Zero Internet Calling**: Operates 100% offline; no external OAuth, Google, or cloud identity dependencies.
3. **Session State Storage**: Token is retained in memory or `sessionStorage`. Browser disconnections preserve existing persistent database import batches, while securing active mutation endpoints.
4. **Air-Gap Banner**: Explicitly confirms that the system is operating within a sealed local enclave.
5. **Zero Public Self-Registration**: In accordance with industrial enclave hardening, the login portal contains strictly no "Sign Up" or self-registration options. All operator accounts are pre-provisioned by a Network Admin.

---

## 4. Master Single Page Application (SPA) Layout Architecture

The overall layout is partitioned into four primary visual zones:

```
+-------------------------------------------------------------------------------------------------------+
| TOP HEADER: Enclave Name | Poller Status | Live UTC Clock | Range Scan | User Role                    |
+-------------------+---------------------------------------------------------------+-------------------+
|                   | TOP METRIC CARDS: Up | Down | Suppressed | High CPU | High RAM    |                   |
|                   +---------------------------------------------------------------+                   |
| LEFT SIDEBAR:     | DUAL CANVAS TABS:                                             | RIGHT SLIDE-OVER: |
| • Dashboard       | [Tab 1: Logical Topology Tree]  [Tab 2: Physical Floorplan]   |                   |
| • Devices Table   |---------------------------------------------------------------| DEVICE INSPECTOR  |
| • Sites Table     |                                                               |                   |
| • Groups Table    |  (Canvas Active Area:                                         | • Reachability    |
| • Staging Grid    |   - Horizontal Backbone (Router -> Core -> Access Switches)   | • CPU / RAM Gauges|
| • Audit Logs      |   - Vertical 2-Branch Endpoints under Access Switches         | • Multi-Volume C/D|
|                   |   - OR Physical Layout blueprint without cables)              | • Net Rx/Tx KB/s  |
|                   |                                                               | • Uplink & Group  |
| (Engine Status)   |                                                               |                   |
+-------------------+---------------------------------------------------------------+-------------------+
```

---

## 5. Top Critical Operational Metric Cards

Directly below the top navigation ribbon, the Dashboard renders five persistent high-contrast HUD cards:

```
+------------------+  +------------------+  +------------------+  +------------------+  +------------------+
| UP CONNECTIONS   |  | DOWN CONNECTIONS |  | SUPPRESSED NODES |  | HIGH CPU (>80%)  |  | HIGH RAM (>80%)  |
| 998 / 1,024      |  | 1 (ACC-SW-A)     |  | 24 (CHILD NODES) |  | 3 SYSTEMS        |  | 5 SYSTEMS        |
| [=========-] 97% |  | Root Outage      |  | 0 Pings Sent     |  | Peak: HIST-DB 91%|  | Peak: MES-01 88% |
| (Emerald Glow)   |  | (Crimson Pulse)  |  | (Amber Glow)     |  | (Cyan Accent)    |  | (Blue Accent)    |
+------------------+  +------------------+  +------------------+  +------------------+  +------------------+
```

### Metrics Definitions & Trigger Thresholds
1. **Up Connections**: Count of devices with `reachability_status = REACHABLE` over total enabled devices. Includes an emerald percentage progress bar.
2. **Down Connections**: Count of primary switches or routers with `reachability_status = UNREACHABLE`. Displays a pulsating crimson dot and root cause hostname callout.
3. **Suppressed Nodes**: Count of child endpoints currently isolated by an upstream switch failure (`status_id = 3 (SUPPRESSED)`). Confirms synthetic ICMP pings are halted.
4. **High CPU Usage**: Real-time counter of monitored hosts whose latest `cpu_utilization_pct > 80.0%`.
5. **High RAM Usage**: Real-time counter of monitored hosts whose latest `ram_utilization_pct > 80.0%`.

---

## 6. Dual-Canvas View Specification

Below the metrics cards, operators toggle between two primary graphical workspaces:

### 6.1 Canvas Tab 1: Logical Topology Tree (Horizontal Width-Wise Backbone + Vertical 2-Branch Endpoints)

The topology tree strictly enforces the physical packet flow of the 4-layer hierarchy. It eliminates horizontal canvas blowout by laying out backbone switches horizontally, and endpoints vertically in two parallel branches:

```
                            [ CORE-ROUTER-01 ] (Width-Wise Root)
                                     |
                                     | (10Gbps Fiber Backbone)
                                     |
                            [ CORE-SWITCH-01 ] (Distribution)
             ________________________|________________________
            |                                                 |
   [ ACCESS-SWITCH-A ]                               [ ACCESS-SWITCH-B ]
    (Zone: Floor 1 Lab)                               (Zone: Floor 2 Ops)
            |                                                 |
      ______|______                                     ______|______
     |             |                                   |             |
+---------+   +---------+                         +---------+   +---------+
| PC-01   |   | PC-05   |                         | PC-09   |   | PC-13   |
+---------+   +---------+                         +---------+   +---------+
| PC-02   |   | PC-06   |                         | PC-10   |   | PC-14   |
+---------+   +---------+                         +---------+   +---------+
| PC-03   |   | PC-07   |                         | PC-11   |   | PC-15   |
+---------+   +---------+                         +---------+   +---------+
| SRV-DB  |   | SRV-GW  |                         | PC-12   |   | SRV-APP |
+---------+   +---------+                         +---------+   +---------+
 (Branch 1)    (Branch 2)                          (Branch 1)    (Branch 2)
```

#### Placement Algorithm & Rules
1. **Backbone Placement (Width-Wise)**:
   * Core Routers and Aggregate Switches are centered horizontally at the top of the canvas.
   * Access Switches are distributed horizontally across the canvas width ($Y = 240\text{px}$).
2. **2-Branch Vertical Endpoint Placement**:
   * For an Access Switch positioned at $(X_{\text{switch}}, Y_{\text{switch}})$, child endpoints are divided equally into **Branch 1 (Left Column)** and **Branch 2 (Right Column)**:
     $$\text{Branch 1 (Left): } X_i = X_{\text{switch}} - \Delta X, \quad Y_i = Y_{\text{trunk}} + (i \cdot \Delta Y)$$
     $$\text{Branch 2 (Right): } X_j = X_{\text{switch}} + \Delta X, \quad Y_j = Y_{\text{trunk}} + (j \cdot \Delta Y)$$
   * $\Delta X = 100\text{px}$ to $120\text{px}$; $\Delta Y = 40\text{px}$ to $45\text{px}$.
   * Prevents wide horizontal trees from overflowing screens; accommodates 24-port and 48-port switches cleanly in compact vertical columns.
3. **Interactive Cascading Failure Simulation**:
   * The canvas toolbar includes a **"Simulate Switch Outage"** action.
   * When clicked, the upstream switch transitions to crimson red (`#f43f5e`), while all downstream branch endpoints immediately transition to amber (`#f59e0b`) with a **`SUPPRESSED`** badge and synthetic pings halted.

---

### 6.2 Canvas Tab 2: Physical Spatial Floorplan Layout [DEFERRED: FUTURE DEVELOPMENT / MILESTONE 2]

> [!NOTE]
> **Implementation Lifecycle Status: Planned for Future Development (Milestone 2)**  
> Canvas Tab 2 (Physical Floorplan) is architecturally defined and ergonomically specified for future development. For the active Phase 1 MVP release, frontend development focuses 100% on **Tab 1 (Logical Topology Tree)**.

The physical spatial view visualizes devices overlaid on architectural floorplans or equipment room diagrams loaded from self-contained JSON files (`layout_schema.json`):

```
+---------------------------------------------------------------------------------+
| SPATIAL REGISTRY: [ 1st Floor Server Lab (floor_1_lab.json) v ]  [ZERO CABLES]  |
+---------------------------------------------------------------------------------+
|  +--------------------------------+       +----------------------------------+  |
|  | ROOM 101: SERVER RACKS         |       | BAY 3: SCADA ROBOTICS & HMI      |  |
|  |                                |       |                                  |  |
|  |  [o] CORE-ROUTER-01 (U24)      |       |  [o] SCADA-PLC-01  [o] HMI-01    |  |
|  |      192.168.1.1               |       |      192.168.1.101     192.168...|  |
|  |                                |       |                                  |  |
|  |  [o] HISTORIAN-DB   (U12)      |       |  [o] SCADA-PLC-02  [o] HMI-02    |  |
|  |      192.168.1.103             |       |      192.168.1.102     192.168...|  |
|  +--------------------------------+       +----------------------------------+  |
+---------------------------------------------------------------------------------+
```

#### Physical Layout Rules
1. **Zero Logical Cables**: Physical floorplans **must never render connection cables or network topology lines**.
2. **Spatial Coordinate Decoupling**: Device $(X,Y)$ coordinates are stored inside external JSON layout files, completely decoupled from the 3NF database schema.
3. **Interactive Binding**: Clicking any device beacon on the architectural floorplan opens the exact same Right Slide-Over Inspector.

---

## 7. Right Slide-Over Device Details & Health Inspector Panel

When an operator clicks any device in the **Topology Tree**, **Physical Floorplan**, or **Tabular Inventory**, a 384px-wide slide-over drawer animates into view from the right margin:

```
+---------------------------------------------+
| [o] HISTORIAN-DB                [ SERVER ] |
| 192.168.1.103                               |
+---------------------------------------------+
| NETWORK REACHABILITY (ICMP/NTP - 5s)        |
| +-------------+ +-------------+ +---------+ |
| | RTT LATENCY | | PACKET LOSS | | NTP DRFT| |
| | 0.5 ms      | | 0.0%        | | +0.3 µs | |
| +-------------+ +-------------+ +---------+ |
+---------------------------------------------+
| HOST OPERATING HEALTH (30s / Port 9182)     |
| [AGENT ONLINE]                              |
|                                             |
| CPU Load (Intel Xeon E-2288G):              |
| 91.2% [==================================-] |
|                                             |
| RAM Utilization:                            |
| 88.4% (56.5 / 64.0 GB)                      |
| [===============================----------] |
|                                             |
| Multi-Volume Disks (disk_volume_samples):   |
| • Drive C: (OS)   52.1% [=========--------] |
|   Free: 245 GB / Total: 512 GB              |
| • Drive D: (Data) 81.4% [==============---] |
|   Free: 380 GB / Total: 2,048 GB            |
|                                             |
| Network Throughput:                         |
| • Rx: 14.8 Mbps    • Tx: 8.2 Mbps           |
+---------------------------------------------+
| HARDWARE & UPLINK IDENTITY                  |
| MAC: 00:1A:2B:A1:01:03                      |
| Group: Operations / Assembly Line A         |
| Uplink: ACCESS-SW-A (192.168.1.10)          |
+---------------------------------------------+
| [ SEND ICMP PING ]    [ HISTORICAL CHARTS ] |
+---------------------------------------------+
```

### Strict N/A Rendering Guarantee
* If the endpoint does not support NTP, the clock drift cell renders **`N/A`** (never `0`).
* If the device is unreachable, RTT renders **`N/A`** (never `0 ms`).
* If an access switch has no host metrics agent, host health renders **`UNSUPPORTED`** with all gauges disabled, while reachability remains independent.

---

## 8. Tabular Registries & CRUD Interaction Standards

For all administrative tables (**Device Inventory**, **Sites Management**, **Groups & Subgroups**, and **Persistent Staging**), the interface adheres to a standardized interaction contract:

```
+---------------------------------------------------------------------------------------+
| DEVICE INVENTORY MATRIX                 [ Search / Filter... ]    [ + ADD DEVICE ]   |
+---------------------------------------------------------------------------------------+
| Status  | Hostname       | Primary IP    | MAC Address       | Group      | Actions   |
|---------+----------------+---------------+-------------------+------------+-----------|
| ONLINE  | CORE-ROUTER-01 | 192.168.1.1   | 00:1A:2B:3C:01:00 | Core Infra | [Edit][Del|
| ONLINE  | HISTORIAN-DB   | 192.168.1.103 | 00:1A:2B:A1:01:03 | DB Cluster | [Edit][Del|
+---------------------------------------------------------------------------------------+
| Showing 1–50 of 1,024 devices | Rows: [ 50 v ] | [|<] [<] [1] [2] [3] ... [21] [>] [>|]|
+---------------------------------------------------------------------------------------+
```

### 8.1 Header Action ("+" Button)
* Located at the **far right edge of the table header banner**, directly aligned with the title.
* Styled with a gradient accent (`bg-gradient-to-r from-cyan-600 to-blue-600`) and a bold `+` icon.
* Triggers the appropriate creation modal dialog (**Create Device**, **Create Site**, or **Create Group**).

### 8.2 Inline Row Actions
Every data row exposes two explicit action icon buttons in the rightmost column:
1. **Edit Action (Pencil Icon)**:
   * Opens an **Edit Configuration Modal**.
   * Pre-populates all existing attributes.
   * **MAC Address Field**: Displayed as disabled/read-only with an explicit notice (`Immutable Hardware Key`).
   * **Primary IP Field**: Fully editable (`Mutable Operational Polling IP`).
2. **Delete Action (Trash Icon)**:
   * Opens an **Orphan-Safe Deletion Confirmation Dialog**.
   * Warns the operator if the target device is an upstream switch.
   * Explains that child devices will be safely preserved as unparented root/orphan nodes (`parent_device_id = NULL`) rather than triggering unintended cascading deletions.

### 8.3 Standardized Tactical Pagination Controls
Every tabular registry (**Device Inventory**, **Sites Management**, **Groups & Subgroups**, **File Staging**, and **Audit Logs**) implements a standardized, high-density tactical pagination footer:

1. **Information Counters**:
   * Displays the exact slice range and global total: `Showing 1–50 of 1,024 records`.
   * Dynamic update when search or group filters are active: `Showing 1–24 of 24 filtered records (from 1,024 total)`.
2. **Page Size Selector**:
   * Dropdown selector with predefined steps: `[ 25 | 50 (Default) | 100 | 250 rows per page ]`.
   * Preserved in local browser preferences (`localStorage`) per tabular view.
3. **Tactical Navigation Bar**:
   * First Page button (`|<`), Previous Page button (`<`).
   * Active and adjacent page pills (`[1]`, `[2]`, `[3]`, ellipsis `...`, `[21]`), with the active page highlighted in bold cyan (`bg-cyan-600 text-white`).
   * Next Page button (`>`), Last Page button (`>|`).
4. **Direct Page Jump (Optional / Power User)**:
   * Compact numeric input allowing operators to type a target page number and press `Enter` to jump instantly.
5. **Backend API Contract (SQL Keyset / Offset Pagination)**:
   * Endpoints accept standard pagination parameters:
     ```http
     GET /api/v1/devices?page=1&limit=50&sort_by=primary_ip&order=asc
     ```
   * Responses return paginated envelopes:
     ```json
     {
       "data": [ /* 50 records */ ],
       "pagination": {
         "total_records": 1024,
         "current_page": 1,
         "page_size": 50,
         "total_pages": 21,
         "has_next": true,
         "has_prev": false
       }
     }
     ```
6. **State Preservation**:
   * Sorting column, sort direction, active page, and search query parameters are reflected in the URL query string, enabling bookmarks and multi-tab workflows.

### 8.4 Frontend Memory Efficiency & DOM Virtualization Directives
To prevent browser memory leaks and ensure smooth 60 FPS scrolling on operator workstations during bulk imports and large inventory views:
1. **Shallow Reactivity Mandate (`shallowRef`)**:
   * All tabular datasets (Device Inventory, Staged Ingestion Records, Audit Logs) must be stored using Vue 3 `shallowRef([])` rather than `ref([])`.
   * Vue avoids creating deep `Proxy` wrappers for thousands of nested cells, reducing browser heap memory by over 80%.
   * When an operator edits an inline cell in the staging grid, only that specific row/field becomes deeply reactive.
2. **Strict Viewport DOM Bounding**:
   * Regardless of whether the database contains 1,000 or 100,000 devices, the DOM must never render more than the active page slice (50 rows default, max 250 rows).
   * For continuous scrolling components, DOM recycling (virtual list windowing) must be employed to reuse existing DOM elements as the operator scrolls.
3. **Flat Memory Footprint Guarantee**:
   * Client-side JavaScript heap memory must remain flat ($< 60\text{ MB}$) even when navigating between 1,000-device topology trees, staging spreadsheets, and audit log histories.

---

## 9. Comprehensive Modal Dialog Specifications

> [!IMPORTANT]
> **Strict Database Schema Alignment Mandate**  
> All modal creation and editing forms reflect **everything in the database schema, nothing more, nothing less**. Every user-configurable column from the corresponding 3NF database table is represented, with zero synthetic or superfluous fields.

---

### 9.1 Provision New Device Modal (`modal-create-device`)
Maps 1:1 to the `devices` table columns:

```
+-------------------------------------------------------------+
| [+] PROVISION NEW NETWORK DEVICE                        [X] |
+-------------------------------------------------------------+
| Display Name / Hostname *:                                  |
| [ SCADA-PLC-04                                            ] |
|                                                             |
| Primary IP Address *:           MAC Address (Hardware Key)*:|
| [ 192.168.1.150               ] [ 00:1A:2B:AA:01:50       ] |
|                                                             |
| Classification (type_id)*:      Operational Group (group_id)|
| [ PC / Workstation          v ] [ Operations / Line A     v ] |
|                                                             |
| Parent Uplink Switch (parent_device_id):                    |
| [ ACCESS-SW-A (192.168.1.10)                              v ] |
|                                                             |
| Hardware Vendor (vendor):       Hardware Model (model):     |
| [ Siemens                     ] [ Simatic S7-1500         ] |
|                                                             |
| Physical Zone (zone):                                       |
| [ Floor 1 Machine Bay 3                                   ] |
|                                                             |
| [X] Enable Active Telemetry Monitoring (is_monitored)       |
+-------------------------------------------------------------+
|                                    [ Cancel ]  [ Provision ]|
+-------------------------------------------------------------+
```

#### Field-to-Schema Mapping (`devices` Table):
| Form Field | DB Column | Data Type | Nullability | Constraints / Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Display Name** | `display_name` | `VARCHAR(150)` | `NOT NULL` | Required hostname or asset label |
| **Primary IP** | `primary_ip` | `VARCHAR(45)` | `NOT NULL` | Mutable operational polling address |
| **MAC Address** | `mac_address` | `VARCHAR(17)` | `NOT NULL UNIQUE` | Immutable hardware natural key (`XX:XX:XX:XX:XX:XX`) |
| **Classification** | `type_id` | `SMALLINT` | `NOT NULL` | FK to `device_types` (`CORE_ROUTER`, `AGGREGATE_SWITCH`, `ACCESS_SWITCH`, `SERVER`, `PC`) |
| **Operational Group** | `group_id` | `INTEGER` | `NOT NULL DEFAULT 1` | FK to `groups`. Defaults to `1` (Unassigned Group) |
| **Parent Switch** | `parent_device_id` | `INTEGER` | `NULL` | Self-referencing FK to `devices`. `NULL` for Core Router |
| **Hardware Vendor** | `vendor` | `VARCHAR(100)` | `NULL` | Inferred from MAC OUI or operator specified |
| **Hardware Model** | `model` | `VARCHAR(100)` | `NULL` | Hardware equipment model |
| **Physical Zone** | `zone` | `VARCHAR(100)` | `NULL` | Access switch or device physical zone label |
| **Monitoring Toggle**| `is_monitored` | `BOOLEAN` | `NOT NULL DEFAULT 1`| Polling toggle (1 = enabled, 0 = disabled) |

---

### 9.2 Edit Device Modal (`modal-edit-device`)
* Displays all 10 schema fields from `devices`.
* **MAC Address (`mac_address`)**: Rendered as **disabled / read-only** with explicit lock badge (`Immutable Hardware Key`).
* **Primary IP (`primary_ip`)**: Fully editable mutable operational attribute. Changing it updates polling targets without breaking foreign keys or time-series history.
* All other fields (`display_name`, `type_id`, `group_id`, `parent_device_id`, `vendor`, `model`, `zone`, `is_monitored`) remain editable.

---

### 9.3 Create / Edit Site Modal (`modal-create-site`)
Maps 1:1 to the `sites` table columns:
* **Site Name (`site_name`)**: `VARCHAR(100) NOT NULL UNIQUE`
* **Facility Code (`facility_code`)**: `VARCHAR(50) NULL` (e.g. `SITE-01`)
* **Description (`description`)**: `TEXT NULL`

---

### 9.4 Create / Edit Group / Subgroup Modal (`modal-create-group`)
Maps 1:1 to the self-referencing `groups` table columns:
* **Group Name (`group_name`)**: `VARCHAR(100) NOT NULL`
* **Parent Group (`parent_group_id`)**: `INTEGER NULL` (Dropdown: `None (Top-Level Group)` or select existing parent group to create a nested `Subgroup`)
* **Site (`site_id`)**: `INTEGER NOT NULL DEFAULT 1`
* **Description (`description`)**: `TEXT NULL`

---

### 9.2 Orphan-Safe Deletion Dialog (`modal-delete-confirm`)

```
+-------------------------------------------------------------+
| [!] CONFIRM DEVICE DELETION                             [X] |
+-------------------------------------------------------------+
| Are you sure you want to delete ACCESS-SW-A (192.168.1.10)? |
|                                                             |
| [!] ORPHAN-SAFE GUARANTEE:                                  |
| Deleting an upstream switch will safely orphan all 24 child |
| devices (parent_device_id = NULL).                          |
| Child devices, their MAC addresses, and historical telemetry|
| samples will NOT be deleted.                                |
+-------------------------------------------------------------+
|                                [ Cancel ]  [ Delete Device ]|
+-------------------------------------------------------------+
```

---

## 10. Performance, Accessibility & Offline Deployment Criteria

1. **Air-Gap Bundling Mandate**:
   * All fonts (`Inter`, `JetBrains Mono`), SVGs, CSS tokens, and JavaScript bundles must be packaged locally in the deployment distribution.
   * Zero calls to external CDNs (`cdnjs`, `google-fonts`, `unpkg`) are permitted.
2. **Canvas Rendering Scale**:
   * Must render up to 1,000 devices at 60 FPS during zoom, drag, and pan operations.
   * Node status diffing: Only mutated nodes undergo DOM/canvas re-renders during 3–5 second polling cycles.
3. **Table Virtualization**:
   * Registries with >200 records must implement virtual scrolling or high-performance pagination (50/100/250 rows per page) to prevent DOM thread locking.
4. **Theme Support**:
   * Default theme is **Tactical Obsidian (Dark)**.
   * High-contrast mode complies with WCAG AA contrast ratios (minimum 4.5:1 for text, 3:1 for graphical UI elements).

---

## 11. Traceability with Upstream Specifications

| UI Component / Guideline | Upstream Requirement | Master Document Reference |
| :--- | :--- | :--- |
| **Cyber-Tactical Theme & Colors** | PRD Aesthetic Vision | PRD v2.1 § 1.1 |
| **Top Critical Metric Cards** | Master SRS Metrics HUD | Master SRS v2.1 § 5.4, 5.6 |
| **2-Branch Vertical Endpoint Layout** | FR-VUE-01 | Master SRS v2.1 § 5.8 & PRD v2.1 § FR-7.1 |
| **Physical Spatial Layout (Zero Cables)**| FR-VUE-02 | Master SRS v2.1 § 5.8 & Schema Spec § 3 |
| **Right Telemetry & Health Inspector** | FR-HST-01 - FR-HST-04 | Master SRS v2.1 § 5.6 & PRD v2.1 § FR-2 |
| **Tabular Registries with Header "+"** | FR-INV-03, FR-GRP-04 | Master SRS v2.1 § 5.1, 5.2 |
| **Orphan-Safe Deletion Confirmation** | FR-INV-05 | Master SRS v2.1 § 5.1 & Schema Spec § 4 |
| **Pre-Dashboard Authentication Portal**| Secure API Surface | PRD v2.1 § FR-9 & Master SRS v2.1 § 5.10 |

---

## 12. Governance, Traceability Protocol & Prototype Status Specification

### 12.1 Absolute Single Source of Truth Mandate
1. **Binding Authority**:
   The four harmonized specification documents (`project_requirements_document.md`, `master_software_requirements_specification.md`, `database_schema_and_ingestion_specification.md`, and `ui_guidelines_and_design_system.md`) constitute the **absolute, immutable single source of truth** for all engineering, design, and implementation work.
2. **Defensive Drift Rejection ("I Cannot Trace This Back to the Document")**:
   Any proposed change, code refactor, schema modification, endpoint adjustment, or UI alteration—regardless of whether it originates from developer ideation, prompt suggestions, or direct user requests—must strictly be traced back to the approved documentation.
   * If an undocumented change is requested, the assistant and engineering team are contractually bound to respond:
     > *"I cannot trace this back to the document / It contradicts the document."*
   * No code or styling changes shall be executed until the documentation is formally updated, cross-referenced, and re-approved.
3. **Auditability & Traceability Lifecycle**:
   Every database table column, API response attribute, and UI visual element must maintain 100% bidirectional traceability against the PRD and SRS requirement IDs.

### 12.2 Prototype Boundary & Implementation Strategy
1. **Binding Skeleton vs. Non-Binding Visual Polish**:
   The standalone interactive prototype (`the_observer_tactical_spa.html`) is approved strictly as a **functional skeleton and spatial layout reference**. Its structural layout and item placements are binding:
   * Header positioning, enclave status indicator, and pre-auth login flow.
   * Top HUD metric cards (Reachable, Outage, Suppressed, High CPU, High RAM).
   * Dual-canvas tabbed switching (Horizontal width-wise router/switch backbone + vertical 2-branch endpoints; Spatial floorplan layout without logical cables).
   * Context-preserving right slide-over inspector for deep-dive node telemetry.
   * Tabular registries with top-right "+" create buttons and pagination controls.
   * 1:1 schema-aligned modal dialogs (*everything in DB schema, nothing more, nothing less*).
2. **Mandatory Production UI/UX Overhaul**:
   The current visual styling of the prototype is recognized as a rough functional draft. The UI/UX will undergo a comprehensive overhaul during production frontend engineering, including:
   * High-fidelity dark tactical theme styling with refined glassmorphism and crisp borders.
   * Professional canvas node rendering (custom SVG iconography, hardware-accelerated canvas/WebGL node renderers).
   * Advanced responsive typography, micro-spacing, and transition physics.
   * Accessible focus indicators, keyboard shortcuts, and industrial alert states.

