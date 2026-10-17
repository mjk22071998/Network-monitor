# REST API Contracts Specification
## System: Network Observer — Industrial LAN Telemetry & Topology Management Platform

**Document Version:** 1.0 (Harmonized Master Baseline)  
**Date:** 16 October 2026  
**Status:** Approved Master Baseline  
**Standard Compliance:** OpenAPI 3.1 / RFC 7231 (HTTP/1.1) / RFC 8259 (JSON)  
**Classification:** Technical / Engineering Interface Specification  
**Target Runtime:** Isolated, Air-Gapped Local Area Network (SCADA / Industrial NOC)

---

> [!IMPORTANT]
> ### Authoritative Governance & Implementation Directives
> 1. **Absolute Single Source of Truth**:
>    This document establishes the authoritative HTTP/REST contracts for all client-to-server and daemon-to-client communication. Every endpoint, request payload, query parameter, response body, HTTP status code, and RBAC guard MUST strictly trace back to this specification, [`project_requirements_document.md`](./project_requirements_document.md), [`master_software_requirements_specification.md`](./master_software_requirements_specification.md), and [`database_schema_and_ingestion_specification.md`](./database_schema_and_ingestion_specification.md).
> 2. **Defensive Drift Rejection**:
>    If any code alteration, additional payload attribute, or route change is requested that is not documented herein, the engineering protocol mandates:
>    > *"I cannot trace this back to the document / It contradicts the document."*
> 3. **Strict 1:1 Database Schema Alignment**:
>    Resource payloads must reflect *everything in the DB schema, nothing more, nothing less*. For devices, this is strictly the 10 columns of the `devices` table.
> 4. **Strict "Never Zero for Missing Data" (N/A) Rule**:
>    Missing, timed-out, or unsupported telemetry metrics (`latency_us`, `ntp_offset_us`, `cpu_utilization_pct`) MUST be serialized as explicit JSON `null`. They are **never converted to zero (`0`)**.
> 5. **Air-Gap Zero Self-Registration**:
>    The API exposes **zero public sign-up endpoints**. All operator accounts are pre-provisioned or created by an authenticated `NETWORK_ADMIN`.

---

## 1. Global Architectural & Protocol Standards

### 1.1 Base URL & Content Negotiation
* **Base URL**: `/api/v1`
* **Transport Protocol**: HTTP/1.1 (or HTTP/2 over TLS for secured LAN deployments).
* **Media Type**: `application/json; charset=utf-8` (Default for all requests and responses, except multipart file uploads).
* **Date-Time Representation**: ISO 8601 extended UTC format (`YYYY-MM-DDTHH:mm:ssZ`).

### 1.2 Authentication & Authorization Headers
Every authenticated request must include the cryptographic bearer token in the standard HTTP header:
```http
Authorization: Bearer <cryptographic_session_token>
```
Unauthenticated requests to protected endpoints return `401 Unauthorized`.  
Requests with valid tokens attempting actions outside their role boundaries return `403 Forbidden`.

### 1.3 Role-Based Access Control (RBAC) Hierarchy
The API enforces three operational roles:
1. **`NETWORK_ADMIN`**: Full administrative governance. User management, site configuration, Core/Aggregate switch CRUD, bulk commit gatekeeper, and audit log inspection.
2. **`NETWORK_ENGINEER`**: Topology, connectivity, Access switch CRUD and uplinks, subnet discovery sweeps, group/subgroup hierarchy, floorplan calibration, staging uploads/grid sanitization, and switch maintenance mode.
3. **`IT_SUPPORT`**: Frontline endpoint maintenance. Leaf device CRUD (PCs, Workstations, Servers, PLCs, Printers), mutable operational attributes (Primary IP, name, zone, parent switch), spatial floorplan placement, viewing live telemetry/host health HUD, and toggling endpoint maintenance mode.

### 1.4 Standardized Error Envelope
When any endpoint encounters a client or server error, it MUST respond with a uniform error payload:

```json
{
  "error": {
    "code": "DEVICE_MAC_IMMUTABLE",
    "message": "The MAC address is an immutable hardware key and cannot be modified once provisioned.",
    "details": {
      "field": "mac_address",
      "rejected_value": "00:1A:2B:3C:4D:5E"
    },
    "timestamp": "2026-10-16T12:00:00Z"
  }
}
```

#### Standard HTTP Status Codes:
| Code | Reason | Meaning in Network Observer |
| :--- | :--- | :--- |
| `200 OK` | Success | Request succeeded; payload returned. |
| `201 Created` | Resource Created | Resource successfully created; location header or created object returned. |
| `204 No Content` | Success (No Body) | Action succeeded (e.g., resource deleted or maintenance mode toggled). |
| `400 Bad Request` | Malformed Request | Syntax error, malformed JSON, or missing required headers. |
| `401 Unauthorized` | Missing / Invalid Token | Missing, invalid, or expired session token. |
| `403 Forbidden` | Insufficient Permissions | Valid session token, but user role does not permit this action. |
| `404 Not Found` | Resource Absent | Target device, group, batch, layout, or user does not exist. |
| `409 Conflict` | Duplicate / Constraint Error | Duplicate MAC address, duplicate site name, or duplicate username. |
| `422 Unprocessable`| Business Logic Violation | Attempting to mutate immutable MAC, invalid IP format, or cyclic parent link. |
| `500 Server Error` | Internal Failure | Unhandled database or system exception. |
| `503 Service Unavail`| Daemon Warming Up | Polling engine or discovery worker temporarily unavailable. |

### 1.5 Standardized Pagination Envelope
All listing endpoints support high-performance keyset/offset pagination matching the UI guidelines (§ 8.3):

#### Query Parameters:
* `page` (integer, default: `1`): The 1-based page index.
* `limit` (integer, default: `50`, allowed: `25`, `50`, `100`, `250`): Maximum rows per page.
* `sort_by` (string, default: `primary_ip`): Target database column to sort by.
* `order` (string, default: `asc`, enum: `asc`, `desc`): Sort order direction.

#### Response Structure:
```json
{
  "data": [
    /* Array of resource records */
  ],
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

---

## 2. Authentication & Session Contracts

### 2.1 Authenticate Operator (Login)
* **Endpoint**: `POST /api/v1/auth/login`
* **Access Level**: Public (Enclave Local Network)
* **Description**: Verifies credentials against `users` table and issues a cryptographically secure session token.
* **Security Directive**: **Zero public sign-up or registration**. Accounts must be pre-provisioned by a `NETWORK_ADMIN`.

#### Request Headers:
```http
Content-Type: application/json
```

#### Request Body:
```json
{
  "username": "sec_operator",
  "password": "CorrectHorseBatteryStaple123!"
}
```

#### Success Response (`200 OK`):
```json
{
  "token": "obs_tok_9f83b27e6a1c4d8e0f2a4c6e8b1d3f5a7c9e1b3d",
  "user": {
    "user_id": 1,
    "username": "sec_operator",
    "role_id": 1,
    "site_id": 1
  },
  "expires_at": "2026-10-17T12:00:00Z"
}
```

#### Error Responses:
* `401 Unauthorized`: Invalid credentials or account deactivated (`is_active = 0`).

---

### 2.2 Terminate Session (Logout)
* **Endpoint**: `POST /api/v1/auth/logout`
* **Access Level**: All Authenticated Roles
* **Description**: Invalidates the active session token in the backend cache and session registry.

#### Request Headers:
```http
Authorization: Bearer <token>
```

#### Success Response (`204 No Content`)

---

### 2.3 Current Identity Context
* **Endpoint**: `GET /api/v1/auth/me`
* **Access Level**: All Authenticated Roles
* **Description**: Returns authenticated user profile, permissions, and session status.

#### Success Response (`200 OK`):
```json
{
  "user_id": 2,
  "username": "j_smith",
  "role_id": 2,
  "site_id": 1,
  "is_active": true,
  "created_at": "2026-10-15T08:30:00Z"
}
```

---

### 2.4 Update Self Passphrase (Change Password)
* **Endpoint**: `PUT /api/v1/auth/password`
* **Access Level**: All Authenticated Roles (`NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`)
* **Description**: Allows any logged-in operator to securely change their own passphrase by verifying their existing current passphrase.

#### Request Headers:
```http
Authorization: Bearer <token>
Content-Type: application/json
```

#### Request Body:
```json
{
  "current_password": "OldSecurePassphrase2026!",
  "new_password": "NewSecretPassphrase2026!"
}
```

#### Success Response (`200 OK`):
```json
{
  "message": "Passphrase updated successfully.",
  "updated_at": "2026-10-16T14:35:00Z"
}
```

#### Error Responses:
* `400 Bad Request`: Password fails complexity/entropy checks (minimum 12 characters).
* `401 Unauthorized`: Current passphrase is incorrect.
* `422 Unprocessable`: New passphrase matches current passphrase (`PASSWORD_REUSE`).

---

## 3. User & Account Management Contracts (Admin Only)

### 3.1 List Operator Accounts
* **Endpoint**: `GET /api/v1/users`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Lists all operator accounts provisioned in the enclave.

#### Success Response (`200 OK`):
```json
{
  "users": [
    {
      "user_id": 1,
      "username": "admin",
      "role": "Network Admin",
      "site_name": "Primary Site Enclave",
      "is_active": true,
      "created_at": "2026-10-15T00:00:00Z"
    },
    {
      "user_id": 2,
      "username": "eng_lead",
      "role": "Network Engineer",
      "site_name": "Primary Site Enclave",
      "is_active": true,
      "created_at": "2026-10-15T08:00:00Z"
    },
    {
      "user_id": 3,
      "username": "it_field",
      "role": "IT Support",
      "site_name": "Primary Site Enclave",
      "is_active": true,
      "created_at": "2026-10-15T09:30:00Z"
    }
  ]
}
```

---

### 3.2 Provision New Operator Account
* **Endpoint**: `POST /api/v1/users`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Creates a new user account with assigned role and initial temporary passphrase.

#### Request Body:
```json
{
  "username": "eng_carter",
  "password": "TemporarySecurePassphrase2026!",
  "role_id": 2,
  "site_id": 1
}
```

#### Success Response (`201 Created`):
```json
{
  "user_id": 4,
  "username": "eng_carter",
  "role_id": 2,
  "site_id": 1,
  "is_active": true,
  "created_at": "2026-10-16T12:30:00Z"
}
```

#### Error Responses:
* `400 Bad Request`: Password fails entropy or length requirements (minimum 12 characters).
* `409 Conflict`: Username already exists in enclave database.
* `422 Unprocessable`: Invalid `role_id` or non-existent `site_id`.

---

### 3.3 Update User Account / Reset Passphrase
* **Endpoint**: `PUT /api/v1/users/{id}`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Updates user role, assigned site, resets passphrase, or toggles `is_active` status.

#### Request Body:
```json
{
  "role_id": 2,
  "site_id": 1,
  "is_active": true,
  "reset_password": "NewTemporaryPassphrase2026!"
}
```

#### Success Response (`200 OK`):
```json
{
  "user_id": 4,
  "username": "eng_carter",
  "role_id": 2,
  "site_id": 1,
  "is_active": true,
  "updated_at": "2026-10-16T13:00:00Z"
}
```

---

### 3.4 Deactivate / Delete User Account
* **Endpoint**: `DELETE /api/v1/users/{id}`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Deactivates or removes user account. Audit logs retain foreign key via `ON DELETE SET NULL`. Prevents self-deletion of current active admin.

#### Success Response (`204 No Content`)

---

## 4. Site Enclave Contracts (Single Site Boundary)

### 4.1 Get Active Site Metadata
* **Endpoint**: `GET /api/v1/site`
* **Access Level**: All Roles (`NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`)
* **Description**: Retrieves single installation enclave configuration (`site_id = 1`).

#### Success Response (`200 OK`):
```json
{
  "site_id": 1,
  "site_name": "Primary Site Enclave",
  "facility_code": "SITE-01",
  "description": "Default industrial air-gapped SCADA LAN facility",
  "created_at": "2026-10-15T00:00:00Z",
  "updated_at": "2026-10-15T00:00:00Z"
}
```

---

### 4.2 Update Active Site Metadata
* **Endpoint**: `PUT /api/v1/site`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Updates installation enclave name, facility code, or operational description.

#### Request Body:
```json
{
  "site_name": "Reactor Unit 4 Control Facility",
  "facility_code": "FAC-R4-SCADA",
  "description": "Sealed industrial SCADA and PLC control network"
}
```

#### Success Response (`200 OK`):
```json
{
  "site_id": 1,
  "site_name": "Reactor Unit 4 Control Facility",
  "facility_code": "FAC-R4-SCADA",
  "description": "Sealed industrial SCADA and PLC control network",
  "updated_at": "2026-10-16T14:00:00Z"
}
```

---

## 5. Groups & Subgroups Contracts (Unified 3NF Hierarchy)

### 5.1 List Groups & Subgroups
* **Endpoint**: `GET /api/v1/groups`
* **Access Level**: All Roles
* **Query Parameters**:
  * `parent_id` (integer, optional): Filter by parent group. If omitted or `null`, returns top-level Groups and their nested Subgroups.

#### Success Response (`200 OK`):
```json
{
  "groups": [
    {
      "group_id": 1,
      "site_id": 1,
      "parent_group_id": null,
      "group_name": "Unassigned Group",
      "description": "System fallback group for uncategorized or orphaned assets",
      "device_count": 5,
      "children": []
    },
    {
      "group_id": 2,
      "site_id": 1,
      "parent_group_id": null,
      "group_name": "Operations",
      "description": "Plant floor manufacturing and telemetry operations",
      "device_count": 0,
      "children": [
        {
          "group_id": 3,
          "site_id": 1,
          "parent_group_id": 2,
          "group_name": "Assembly Line A",
          "description": "PLC controllers and HMIs on primary assembly line",
          "device_count": 28,
          "children": []
        },
        {
          "group_id": 4,
          "site_id": 1,
          "parent_group_id": 2,
          "group_name": "Assembly Line B",
          "description": "Packaging and robotics telemetry controllers",
          "device_count": 24,
          "children": []
        }
      ]
    }
  ]
}
```

---

### 5.2 Create Group or Subgroup
* **Endpoint**: `POST /api/v1/groups`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Description**: Creates a new organizational tier. Maps 1:1 to `groups` table columns.

#### Request Body:
```json
{
  "group_name": "Quality Control Testing",
  "parent_group_id": 2,
  "description": "Inspection cameras and diagnostic sensors"
}
```

#### Success Response (`201 Created`):
```json
{
  "group_id": 5,
  "site_id": 1,
  "parent_group_id": 2,
  "group_name": "Quality Control Testing",
  "description": "Inspection cameras and diagnostic sensors",
  "created_at": "2026-10-16T14:15:00Z"
}
```

---

### 5.3 Update Group or Subgroup
* **Endpoint**: `PUT /api/v1/groups/{id}`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Description**: Renames, moves, or updates descriptions for an existing group or subgroup. Cannot set `parent_group_id` to itself or create cyclic dependencies.

#### Request Body:
```json
{
  "group_name": "Quality Control & Metrology",
  "parent_group_id": 2,
  "description": "Updated QA lab sensor network"
}
```

#### Success Response (`200 OK`):
```json
{
  "group_id": 5,
  "site_id": 1,
  "parent_group_id": 2,
  "group_name": "Quality Control & Metrology",
  "description": "Updated QA lab sensor network",
  "created_at": "2026-10-16T14:15:00Z"
}
```

---

### 5.4 Delete Group or Subgroup
* **Endpoint**: `DELETE /api/v1/groups/{id}`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Description**: Safely removes a group. Cannot delete `group_id = 1` (Unassigned Group). Member devices are automatically re-assigned to `group_id = 1`. Subgroups are reparented or reassigned safely.

#### Success Response (`200 OK`):
```json
{
  "deleted_group_id": 5,
  "reassigned_devices_count": 8,
  "target_group_id": 1,
  "target_group_name": "Unassigned Group"
}
```

---

## 6. Device Inventory Management Contracts (1:1 Schema Mapping)

### 6.1 List Devices (Paginated & Filterable)
* **Endpoint**: `GET /api/v1/devices`
* **Access Level**: All Roles
* **Query Parameters**:
  * `page` (int, default: 1)
  * `limit` (int, default: 50)
  * `group_id` (int, optional)
  * `type_id` (int, optional)
  * `zone` (string, optional)
  * `status_id` (int, optional)
  * `search` (string, optional: matches `display_name`, `primary_ip`, or `mac_address`)
  * `sort_by` (string, default: `primary_ip`)
  * `order` (string, default: `asc`)

#### Success Response (`200 OK`):
```json
{
  "data": [
    {
      "device_id": 10,
      "mac_address": "00:1A:2B:AA:01:05",
      "primary_ip": "192.168.1.101",
      "display_name": "PLC-LINE-A-01",
      "type_id": 5,
      "type_code": "PC",
      "group_id": 3,
      "group_name": "Assembly Line A",
      "parent_device_id": 3,
      "parent_device_name": "ACCESS-SW-A",
      "vendor": "Siemens",
      "model": "Simatic S7-1500",
      "zone": "Floor 1 Machine Bay 3",
      "is_monitored": true,
      "latest_reachability": {
        "status_id": 1,
        "status_code": "REACHABLE",
        "latency_us": 1240,
        "loss_percentage": 0.0,
        "ntp_offset_us": 400,
        "is_suppressed": false,
        "recorded_at": "2026-10-16T14:20:00Z"
      },
      "latest_health": {
        "status_id": 1,
        "status_code": "AVAILABLE",
        "cpu_utilization_pct": 14.5,
        "ram_utilization_pct": 42.1,
        "recorded_at": "2026-10-16T14:19:30Z"
      }
    }
  ],
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

---

### 6.2 Get Single Device Details
* **Endpoint**: `GET /api/v1/devices/{id}`
* **Access Level**: All Roles
* **Query Parameters**:
  * Alternative lookup supported via query parameter: `GET /api/v1/devices?mac=00:1A:2B:AA:01:05`

#### Success Response (`200 OK`):
Returns complete device object matching Section 6.1 with attached parent link details and volume allocations.

---

### 6.3 Provision New Device
* **Endpoint**: `POST /api/v1/devices`
* **Access Level (Tiered Guard)**:
  * Core Routers / Aggregate Switches: `NETWORK_ADMIN` exclusively.
  * Access Switches: `NETWORK_ADMIN`, `NETWORK_ENGINEER`.
  * Leaf Endpoints (PCs, Servers, PLCs, Printers): `NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`.
* **Field Mapping**: Reflects strictly the 10 fields of `devices` table.

#### Request Body:
```json
{
  "display_name": "PLC-LINE-A-04",
  "primary_ip": "192.168.1.104",
  "mac_address": "00:1A:2B:AA:01:08",
  "type_id": 5,
  "group_id": 3,
  "parent_device_id": 3,
  "vendor": "Siemens",
  "model": "Simatic S7-1500",
  "zone": "Floor 1 Machine Bay 3",
  "is_monitored": true
}
```

#### Success Response (`201 Created`):
```json
{
  "device_id": 54,
  "mac_address": "00:1A:2B:AA:01:08",
  "primary_ip": "192.168.1.104",
  "display_name": "PLC-LINE-A-04",
  "type_id": 5,
  "group_id": 3,
  "parent_device_id": 3,
  "vendor": "Siemens",
  "model": "Simatic S7-1500",
  "zone": "Floor 1 Machine Bay 3",
  "is_monitored": true,
  "created_at": "2026-10-16T14:22:00Z"
}
```

#### Error Responses:
* `403 Forbidden`: `IT_SUPPORT` attempting to create a switch or router.
* `409 Conflict`: MAC address already exists in `devices` table.
* `422 Unprocessable`: Invalid IPv4/IPv6 address syntax, invalid MAC address format, or non-existent `group_id` / `parent_device_id`.

---

### 6.4 Update Device Attributes
* **Endpoint**: `PUT /api/v1/devices/{id}`
* **Access Level (Tiered Guard)**: Same role tiering as Section 6.3.
* **Hardware Immutability Guarantee**: `mac_address` CANNOT be edited. If submitted, backend validates that it equals the stored MAC address; attempting to mutate it returns `422 Unprocessable`.

#### Request Body:
```json
{
  "display_name": "PLC-LINE-A-04-REV2",
  "primary_ip": "192.168.1.120",
  "type_id": 5,
  "group_id": 3,
  "parent_device_id": 3,
  "vendor": "Siemens",
  "model": "Simatic S7-1500",
  "zone": "Floor 1 Machine Bay 4",
  "is_monitored": true
}
```

#### Success Response (`200 OK`):
```json
{
  "device_id": 54,
  "mac_address": "00:1A:2B:AA:01:08",
  "primary_ip": "192.168.1.120",
  "display_name": "PLC-LINE-A-04-REV2",
  "type_id": 5,
  "group_id": 3,
  "parent_device_id": 3,
  "vendor": "Siemens",
  "model": "Simatic S7-1500",
  "zone": "Floor 1 Machine Bay 4",
  "is_monitored": true,
  "updated_at": "2026-10-16T14:25:00Z"
}
```

#### Error Responses:
* `422 Unprocessable`: Client attempts to mutate `mac_address`. Returns error code `DEVICE_MAC_IMMUTABLE`.

---

### 6.5 Safe Orphan Device Deletion
* **Endpoint**: `DELETE /api/v1/devices/{id}`
* **Access Level (Tiered Guard)**:
  * Core Routers / Aggregate Switches: `NETWORK_ADMIN` exclusively.
  * Access Switches: `NETWORK_ADMIN`, `NETWORK_ENGINEER`.
  * Leaf Endpoints: `NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`.
* **Orphan-Safe Guarantee**: Deleting an upstream switch safely orphans child devices (`parent_device_id = NULL`). Child records, MAC addresses, and historical telemetry samples are NOT deleted.

#### Success Response (`200 OK`):
```json
{
  "deleted_device_id": 3,
  "deleted_display_name": "ACCESS-SW-A",
  "orphaned_children_count": 24,
  "orphaned_children_device_ids": [10, 11, 12, 13, 14, 15]
}
```

---

## 7. Topology & Telemetry HUD Contracts

### 7.1 Fetch Hierarchical Topology Tree
* **Endpoint**: `GET /api/v1/topology`
* **Access Level**: All Roles
* **Description**: Returns the complete logical graph structure and cached reachability statuses for canvas rendering.
* **2-Branch Endpoint Layout Guarantee**: Access switch children are partitioned into 2 parallel vertical branches (`left_branch`, `right_branch`) to prevent horizontal canvas spread.

#### Success Response (`200 OK`):
```json
{
  "generated_at": "2026-10-16T14:30:00Z",
  "root_nodes": [
    {
      "device_id": 1,
      "mac_address": "00:1A:2B:00:00:01",
      "primary_ip": "10.0.0.1",
      "display_name": "CORE-ROUTER-01",
      "type_code": "CORE_ROUTER",
      "status_code": "REACHABLE",
      "latency_us": 420,
      "children": [
        {
          "device_id": 2,
          "mac_address": "00:1A:2B:00:00:02",
          "primary_ip": "10.0.0.2",
          "display_name": "AGG-SWITCH-01",
          "type_code": "AGGREGATE_SWITCH",
          "status_code": "REACHABLE",
          "latency_us": 680,
          "children": [
            {
              "device_id": 3,
              "mac_address": "00:1A:2B:00:00:10",
              "primary_ip": "192.168.1.10",
              "display_name": "ACCESS-SW-A",
              "type_code": "ACCESS_SWITCH",
              "status_code": "REACHABLE",
              "latency_us": 850,
              "endpoint_branches": {
                "branch_left": [
                  {
                    "device_id": 10,
                    "primary_ip": "192.168.1.101",
                    "display_name": "PLC-LINE-A-01",
                    "status_code": "REACHABLE",
                    "latency_us": 1240,
                    "branch_index": 0
                  }
                ],
                "branch_right": [
                  {
                    "device_id": 11,
                    "primary_ip": "192.168.1.102",
                    "display_name": "PLC-LINE-A-02",
                    "status_code": "REACHABLE",
                    "latency_us": 1180,
                    "branch_index": 0
                  }
                ]
              }
            }
          ]
        }
      ]
    }
  ]
}
```

---

### 7.2 Top HUD Critical Metrics Summary
* **Endpoint**: `GET /api/v1/telemetry/summary`
* **Access Level**: All Roles
* **Description**: Returns live operational counters displayed in the top dashboard HUD cards.

#### Success Response (`200 OK`):
```json
{
  "timestamp": "2026-10-16T14:30:05Z",
  "reachability": {
    "total_monitored": 1024,
    "reachable_count": 982,
    "outage_count": 2,
    "suppressed_count": 40,
    "unknown_count": 0
  },
  "host_health": {
    "total_agents": 850,
    "high_cpu_count": 4,
    "high_ram_count": 7,
    "storage_warning_count": 1
  }
}
```

---

### 7.3 Slide-Over Telemetry Inspector (Live Node Details)
* **Endpoint**: `GET /api/v1/devices/{id}/telemetry/live`
* **Access Level**: All Roles
* **Description**: Delivers complete decoupled network and host metrics for the right-hand slide-over inspector.
* **Strict N/A Rule**: Missing or timed-out metrics return explicit `null`.

#### Success Response (`200 OK`):
```json
{
  "device_id": 10,
  "mac_address": "00:1A:2B:AA:01:05",
  "primary_ip": "192.168.1.101",
  "display_name": "PLC-LINE-A-01",
  "network_layer": {
    "status_code": "REACHABLE",
    "latency_us": 1240,
    "latency_ms": 1.24,
    "loss_percentage": 0.0,
    "ntp_offset_us": 400,
    "ntp_offset_ms": 0.40,
    "is_suppressed": false,
    "last_checked_at": "2026-10-16T14:30:00Z"
  },
  "host_layer": {
    "status_code": "AVAILABLE",
    "cpu_utilization_pct": 14.5,
    "ram_utilization_pct": 42.1,
    "ram_used_bytes": 3617062912,
    "ram_total_bytes": 8589934592,
    "network_in_bps": 450200,
    "network_out_bps": 120500,
    "last_reported_at": "2026-10-16T14:29:45Z",
    "disk_volumes": [
      {
        "volume_name": "C:",
        "total_bytes": 256060514304,
        "used_bytes": 108447924224,
        "used_pct": 42.35
      },
      {
        "volume_name": "D: (DATA)",
        "total_bytes": 1099511627776,
        "used_bytes": 824633720832,
        "used_pct": 75.00
      }
    ]
  }
}
```

---

### 7.4 Historical Telemetry Time-Series Queries
* **Endpoint**: `GET /api/v1/devices/{id}/telemetry/history`
* **Access Level**: All Roles
* **Query Parameters**:
  * `start` (ISO 8601 string, required): Start window timestamp.
  * `end` (ISO 8601 string, required): End window timestamp.
  * `interval` (string, default: `auto`, enum: `raw`, `1m`, `5m`, `1h`): Rollup bucket interval.

#### Success Response (`200 OK`):
```json
{
  "device_id": 10,
  "start": "2026-10-16T13:30:00Z",
  "end": "2026-10-16T14:30:00Z",
  "samples": [
    {
      "recorded_at": "2026-10-16T13:30:00Z",
      "status_code": "REACHABLE",
      "latency_us": 1210,
      "ntp_offset_us": 380,
      "cpu_utilization_pct": 12.0,
      "ram_utilization_pct": 41.8
    },
    {
      "recorded_at": "2026-10-16T13:35:00Z",
      "status_code": "UNREACHABLE",
      "latency_us": null,
      "ntp_offset_us": null,
      "cpu_utilization_pct": null,
      "ram_utilization_pct": null
    }
  ]
}
```

---

### 7.5 Get Monitoring Engine Configuration
* **Endpoint**: `GET /api/v1/monitoring/config`
* **Access Level**: All Roles (`NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`)
* **Description**: Returns the active global polling cadences, timeouts, and cascading suppression state.

#### Success Response (`200 OK`):
```json
{
  "config_id": 1,
  "site_id": 1,
  "reachability_interval_sec": 5,
  "reachability_timeout_ms": 2000,
  "health_interval_sec": 30,
  "health_timeout_ms": 5000,
  "cascade_suppression_enabled": true,
  "updated_at": "2026-10-16T12:00:00Z"
}
```

---

### 7.6 Update Monitoring Engine Configuration
* **Endpoint**: `PUT /api/v1/monitoring/config`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER` (Denied 403 for `IT_SUPPORT`)
* **Description**: Updates global polling cadences, timeouts, and toggles cascade suppression.

#### Request Body:
```json
{
  "reachability_interval_sec": 5,
  "reachability_timeout_ms": 2000,
  "health_interval_sec": 30,
  "health_timeout_ms": 5000,
  "cascade_suppression_enabled": true
}
```

#### Success Response (`200 OK`):
```json
{
  "config_id": 1,
  "site_id": 1,
  "reachability_interval_sec": 5,
  "reachability_timeout_ms": 2000,
  "health_interval_sec": 30,
  "health_timeout_ms": 5000,
  "cascade_suppression_enabled": true,
  "updated_at": "2026-10-16T15:00:00Z"
}
```

#### Error Responses:
* `400 Bad Request`: `reachability_interval_sec` out of range (1–60s) or `health_interval_sec` out of range (10–300s).
* `403 Forbidden`: `IT_SUPPORT` attempting to modify engine polling intervals.

---

## 8. Operational Actions & Diagnostic Contracts

### 8.1 Trigger Subnet Discovery Scan
* **Endpoint**: `POST /api/v1/discovery/scan`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER` (Denied 403 for `IT_SUPPORT`)
* **Description**: Dispatches an asynchronous subnet sweep (ICMP sweep, ARP cache read, SNMP switch bridge interrogation).

#### Request Body:
```json
{
  "subnet_cidr": "192.168.1.0/24",
  "snmp_community": "public",
  "snmp_version": "v2c"
}
```

#### Success Response (`202 Accepted`):
```json
{
  "scan_id": "scan_8b1a3c5d7e",
  "status": "RUNNING",
  "target_cidr": "192.168.1.0/24",
  "total_ips": 254,
  "started_at": "2026-10-16T14:35:00Z"
}
```

---

### 8.2 Check Subnet Discovery Status
* **Endpoint**: `GET /api/v1/discovery/scan/{scan_id}`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`

#### Success Response (`200 OK`):
```json
{
  "scan_id": "scan_8b1a3c5d7e",
  "status": "COMPLETED",
  "progress_pct": 100.0,
  "discovered_assets": [
    {
      "primary_ip": "192.168.1.45",
      "mac_address": "00:1A:2B:EE:02:11",
      "vendor": "Siemens",
      "inferred_type": "ACCESS_SWITCH",
      "is_already_registered": false
    }
  ]
}
```

---

### 8.3 Toggle Device Maintenance Mode
* **Endpoint**: `PUT /api/v1/devices/{id}/maintenance`
* **Access Level**:
  * Switches & Routers: `NETWORK_ADMIN`, `NETWORK_ENGINEER`.
  * Leaf Endpoints: `NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`.

#### Request Body:
```json
{
  "is_monitored": false,
  "reason": "Scheduled PLC firmware update"
}
```

#### Success Response (`200 OK`):
```json
{
  "device_id": 10,
  "is_monitored": false,
  "status_code": "UNKNOWN",
  "audit_log_id": 842
}
```

---

## 9. Bulk File Ingestion Pipeline Contracts (CSV / SQLite) [PROVISIONAL: SUBJECT TO REVISION DURING DEVELOPMENT]

> [!NOTE]
> **Implementation Lifecycle Status: Provisional (Not Final)**  
> The endpoints, staging payload structures, validation rules, and resolution models below represent the initial architectural baseline. Per project directives, this structure is marked as **provisional** and will be finalized during active development when live data fixtures, import scenarios, and full pipeline images are exercised. Any modifications will be documented here first before coding.

### 9.1 Upload Bulk File to Persistent Staging
* **Endpoint**: `POST /api/v1/ingest/upload`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Description**: Multipart file upload. Backend parses file, inserts rows into `import_batches` and `staged_devices`, executes automated pre-resolution, and flags rows.
* **Resilience Directive**: Batches persist in SQLite database tables across daemon restarts and browser closures.

#### Request Headers:
```http
Content-Type: multipart/form-data
```

#### Success Response (`201 Created`):
```json
{
  "batch_id": 12,
  "original_filename": "plant_floor_inventory_oct2026.csv",
  "source_file_type": "CSV",
  "total_rows": 150,
  "valid_rows_count": 138,
  "incomplete_rows_count": 10,
  "conflict_rows_count": 2,
  "status": "STAGED",
  "created_at": "2026-10-16T14:40:00Z"
}
```

---

### 9.2 List Persistent Staging Batches
* **Endpoint**: `GET /api/v1/ingest/batches`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`

#### Success Response (`200 OK`):
```json
{
  "batches": [
    {
      "batch_id": 12,
      "original_filename": "plant_floor_inventory_oct2026.csv",
      "source_file_type": "CSV",
      "total_rows": 150,
      "status": "STAGED",
      "uploaded_by_user_id": 2,
      "created_at": "2026-10-16T14:40:00Z"
    }
  ]
}
```

---

### 9.3 Get Staged Records for Interactive Grid
* **Endpoint**: `GET /api/v1/ingest/batches/{batch_id}/records`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Query Parameters**:
  * `status` (string, optional: `VALID`, `INCOMPLETE`, `CONFLICT`)
  * `page` (int, default: 1)
  * `limit` (int, default: 50)

#### Success Response (`200 OK`):
```json
{
  "batch_id": 12,
  "data": [
    {
      "staged_id": 145,
      "source_row_number": 24,
      "raw_mac_address": null,
      "raw_primary_ip": "192.168.1.180",
      "raw_display_name": "HMI-BAY-4",
      "raw_device_type": "Workstation",
      "raw_group_path": "Operations / Line B",
      "raw_parent_identifier": "192.168.1.10",
      "raw_zone": "Line B Packaging",
      "raw_vendor": "Advantech",
      "raw_model": "PPC-3150",
      "resolved_type_id": 5,
      "resolved_group_id": 4,
      "resolved_parent_device_id": 3,
      "validation_status": "INCOMPLETE",
      "validation_errors": "Missing required MAC Address. Please enter hardware key.",
      "is_user_modified": false
    }
  ],
  "pagination": {
    "total_records": 150,
    "current_page": 1,
    "page_size": 50,
    "total_pages": 3,
    "has_next": true,
    "has_prev": false
  }
}
```

---

### 9.4 Inline Cell Edit on Staged Record
* **Endpoint**: `PUT /api/v1/ingest/records/{staged_id}`
* **Access Level**: `NETWORK_ADMIN`, `NETWORK_ENGINEER`
* **Description**: Allows operators to edit incomplete or conflicting cells in the web staging grid. Automatically re-validates the row.

#### Request Body:
```json
{
  "raw_mac_address": "00:1A:2B:CC:04:88",
  "raw_parent_identifier": "192.168.1.10"
}
```

#### Success Response (`200 OK`):
```json
{
  "staged_id": 145,
  "raw_mac_address": "00:1A:2B:CC:04:88",
  "validation_status": "VALID",
  "validation_errors": null,
  "is_user_modified": true
}
```

---

### 9.5 Commit Staged Batch to Production (Gatekeeper)
* **Endpoint**: `POST /api/v1/ingest/batches/{batch_id}/commit`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Atomically commits valid staged records into live `devices` within an ACID transaction.
* **Commit Options**: Accepts `commit_valid_only: true` to commit valid rows and retain incomplete rows for future resolution.

#### Request Body:
```json
{
  "commit_valid_only": true
}
```

#### Success Response (`200 OK`):
```json
{
  "batch_id": 12,
  "status": "COMMITTED",
  "committed_rows_count": 149,
  "remaining_incomplete_count": 1,
  "committed_at": "2026-10-16T14:45:00Z"
}
```

#### Error Responses:
* `403 Forbidden`: `NETWORK_ENGINEER` or `IT_SUPPORT` attempting to commit batch.
* `409 Conflict`: Unresolved MAC address collision detected during transaction; entire transaction is rolled back (`ROLLBACK`).

---

### 9.6 Discard Staging Batch
* **Endpoint**: `DELETE /api/v1/ingest/batches/{batch_id}`
* **Access Level**: `NETWORK_ADMIN` exclusively
* **Description**: Purges all staged rows and drops the staging batch.

#### Success Response (`204 No Content`)

---

## 10. Physical Spatial Layout Contracts [DEFERRED: FUTURE DEVELOPMENT / MILESTONE 2]

> [!NOTE]
> **Implementation Lifecycle Status: Planned for Future Development (Milestone 2)**  
> The `/api/v1/layouts/...` contracts and data models are defined and preserved herein to ensure future architectural stability. Active Phase 1 MVP runtime focuses exclusively on the Logical Topology Tree.

### 10.1 List Registered Layouts
* **Endpoint**: `GET /api/v1/layouts`
* **Access Level**: All Roles

#### Success Response (`200 OK`):
```json
{
  "layouts": [
    {
      "layout_id": 1,
      "site_id": 1,
      "layout_name": "Floor 1 Production Hall Blueprint",
      "file_path": "layouts/floor1_production_hall.json",
      "image_path": "floorplans/floor1_cad_blueprint.svg",
      "created_at": "2026-10-15T00:00:00Z"
    }
  ]
}
```

---

### 10.2 Fetch Layout Spatial Coordinates & Blueprint
* **Endpoint**: `GET /api/v1/layouts/{id}`
* **Access Level**: All Roles
* **Rendering Directive**: Physical layout renders spatial beacons directly over the floorplan image. **Zero logical cable lines are drawn**.

#### Success Response (`200 OK`):
```json
{
  "layout_id": 1,
  "layout_name": "Floor 1 Production Hall Blueprint",
  "image_path": "floorplans/floor1_cad_blueprint.svg",
  "canvas": {
    "width": 1920,
    "height": 1080
  },
  "placements": [
    {
      "device_id": 3,
      "mac_address": "00:1A:2B:00:00:10",
      "primary_ip": "192.168.1.10",
      "display_name": "ACCESS-SW-A",
      "type_code": "ACCESS_SWITCH",
      "x": 450,
      "y": 280,
      "status_code": "REACHABLE",
      "custom_label": "Switch Cabinet Alpha"
    },
    {
      "device_id": 10,
      "mac_address": "00:1A:2B:AA:01:05",
      "primary_ip": "192.168.1.101",
      "display_name": "PLC-LINE-A-01",
      "type_code": "PC",
      "x": 480,
      "y": 420,
      "status_code": "REACHABLE",
      "custom_label": "Machine Bay 3 PLC"
    }
  ]
}
```

---

### 10.3 Update Layout Device Coordinate Placements
* **Endpoint**: `PUT /api/v1/layouts/{id}/placements`
* **Access Level**:
  * Switches: `NETWORK_ADMIN`, `NETWORK_ENGINEER`.
  * Leaf Endpoints: `NETWORK_ADMIN`, `NETWORK_ENGINEER`, `IT_SUPPORT`.

#### Request Body:
```json
{
  "placements": [
    {
      "device_id": 10,
      "x": 510,
      "y": 440,
      "custom_label": "Machine Bay 3 PLC (Relocated)"
    }
  ]
}
```

#### Success Response (`200 OK`):
```json
{
  "layout_id": 1,
  "updated_placements_count": 1,
  "updated_at": "2026-10-16T14:50:00Z"
}
```

---

## 11. Security Audit Trail Contracts (Admin Only)

### 11.1 Query Immutable Audit Log
* **Endpoint**: `GET /api/v1/audit/logs`
* **Access Level**: `NETWORK_ADMIN` exclusively (Denied 403 for `NETWORK_ENGINEER` and `IT_SUPPORT`)
* **Query Parameters**:
  * `page` (int, default: 1)
  * `limit` (int, default: 50)
  * `user_id` (int, optional)
  * `action_code` (string, optional: `DEVICE_CREATE`, `IP_UPDATE`, `BATCH_COMMIT`, etc.)
  * `start` (ISO 8601 string, optional)
  * `end` (ISO 8601 string, optional)

#### Success Response (`200 OK`):
```json
{
  "data": [
    {
      "log_id": 842,
      "user_id": 1,
      "username": "admin",
      "action_code": "BATCH_COMMIT",
      "entity_type": "BATCH",
      "entity_id": "12",
      "details": "Committed 149 valid records from plant_floor_inventory_oct2026.csv into production devices table.",
      "logged_at": "2026-10-16T14:45:00Z"
    },
    {
      "log_id": 841,
      "user_id": 3,
      "username": "it_field",
      "action_code": "IP_UPDATE",
      "entity_type": "DEVICE",
      "entity_id": "54",
      "details": "Updated Primary IP from 192.168.1.104 to 192.168.1.120. MAC address remained locked.",
      "logged_at": "2026-10-16T14:25:00Z"
    }
  ],
  "pagination": {
    "total_records": 842,
    "current_page": 1,
    "page_size": 50,
    "total_pages": 17,
    "has_next": true,
    "has_prev": false
  }
}
```

---

## 12. Master API Route Authorization Matrix

| HTTP Method | Route Endpoint | Authorized Role(s) | Primary Success Code | Access Violation Code |
| :--- | :--- | :--- | :---: | :---: |
| `POST` | `/api/v1/auth/login` | Enclave Public (No Auth Required) | `200 OK` | `401 Unauthorized` |
| `POST` | `/api/v1/auth/logout` | All Roles | `204 No Content`| `401 Unauthorized` |
| `GET` | `/api/v1/auth/me` | All Roles | `200 OK` | `401 Unauthorized` |
| `PUT` | `/api/v1/auth/password` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/users` | `NETWORK_ADMIN` | `200 OK` | `403 Forbidden` |
| `POST` | `/api/v1/users` | `NETWORK_ADMIN` | `201 Created` | `403 Forbidden` |
| `PUT` | `/api/v1/users/{id}` | `NETWORK_ADMIN` | `200 OK` | `403 Forbidden` |
| `DELETE`| `/api/v1/users/{id}` | `NETWORK_ADMIN` | `204 No Content`| `403 Forbidden` |
| `GET` | `/api/v1/site` | All Roles | `200 OK` | `401 Unauthorized` |
| `PUT` | `/api/v1/site` | `NETWORK_ADMIN` | `200 OK` | `403 Forbidden` |
| `GET` | `/api/v1/groups` | All Roles | `200 OK` | `401 Unauthorized` |
| `POST` | `/api/v1/groups` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `201 Created` | `403 Forbidden` |
| `PUT` | `/api/v1/groups/{id}` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `DELETE`| `/api/v1/groups/{id}` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `GET` | `/api/v1/devices` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/devices/{id}`| All Roles | `200 OK` | `401 Unauthorized` |
| `POST` | `/api/v1/devices` | Admin (Backbone), Engineer (Switches), Support (Leaf) | `201 Created` | `403 Forbidden` |
| `PUT` | `/api/v1/devices/{id}`| Admin (Backbone), Engineer (Switches), Support (Leaf) | `200 OK` | `403 Forbidden` |
| `DELETE`| `/api/v1/devices/{id}`| Admin (Backbone), Engineer (Switches), Support (Leaf) | `200 OK` | `403 Forbidden` |
| `GET` | `/api/v1/topology` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/telemetry/summary`| All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/devices/{id}/telemetry/live` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/devices/{id}/telemetry/history` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/monitoring/config` | All Roles | `200 OK` | `401 Unauthorized` |
| `PUT` | `/api/v1/monitoring/config` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `POST` | `/api/v1/discovery/scan` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `202 Accepted` | `403 Forbidden` |
| `GET` | `/api/v1/discovery/scan/{id}` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `PUT` | `/api/v1/devices/{id}/maintenance` | Admin & Eng (Any node), Support (Leaf nodes) | `200 OK` | `403 Forbidden` |
| `POST` | `/api/v1/ingest/upload` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `201 Created` | `403 Forbidden` |
| `GET` | `/api/v1/ingest/batches` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `GET` | `/api/v1/ingest/batches/{id}/records` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `PUT` | `/api/v1/ingest/records/{id}` | `NETWORK_ADMIN`, `NETWORK_ENGINEER` | `200 OK` | `403 Forbidden` |
| `POST` | `/api/v1/ingest/batches/{id}/commit` | `NETWORK_ADMIN` exclusively | `200 OK` | `403 Forbidden` |
| `DELETE`| `/api/v1/ingest/batches/{id}` | `NETWORK_ADMIN` exclusively | `204 No Content`| `403 Forbidden` |
| `GET` | `/api/v1/layouts` | All Roles | `200 OK` | `401 Unauthorized` |
| `GET` | `/api/v1/layouts/{id}` | All Roles | `200 OK` | `401 Unauthorized` |
| `PUT` | `/api/v1/layouts/{id}/placements` | Admin & Eng (All), Support (Leaf nodes) | `200 OK` | `403 Forbidden` |
| `GET` | `/api/v1/audit/logs` | `NETWORK_ADMIN` exclusively | `200 OK` | `403 Forbidden` |

---
