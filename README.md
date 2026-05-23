# 🚀 Project Management API — Enterprise Backend (.NET 9)

A **production-grade backend system** built using **ASP.NET Core Web API** with **Clean Architecture**, **CQRS**, and **Domain-Driven Design (DDD)**.

This project demonstrates **real-world backend engineering practices** including:

* Secure authentication & authorization (JWT + RBAC)
* High-performance caching (Redis)
* Scalable architecture
* Full test coverage
* Dockerized deployment
* Structured logging & observability

---

## 📌 Table of Contents

* Overview
* Architecture
* Tech Stack
* Features
* Security & Authorization
* Caching Strategy
* API Versioning
* API Testing Guide (Swagger)
* Sample Requests & Responses
* Running the Project
* Docker Setup
* Logging & Monitoring
* Testing
* Design Decisions
* Author

---

## 📌 Overview

A **Project & Task Management API** where users can:

* Create and manage projects
* Manage tasks within projects
* Secure data using role-based access control
* Scale efficiently using Redis caching

---

## 🧱 Architecture

```
src/
├── ProjectManagement.API            → Controllers, Middleware
├── ProjectManagement.Application    → CQRS, Handlers, Validators
├── ProjectManagement.Domain         → Entities, Enums, Business Rules
├── ProjectManagement.Infrastructure → EF Core, Identity, Redis
└── ProjectManagement.Shared         → ApiResponse, Helpers
```

### 🔥 Key Principles

* Clean Architecture (Strict Layer Separation)
* Dependency Injection
* SOLID Principles
* CQRS with MediatR
* Domain-Driven Design

---

## ⚙️ Tech Stack

* .NET 9
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server
* ASP.NET Identity
* JWT Authentication
* Redis (Cache-Aside)
* MediatR
* FluentValidation
* Serilog
* Docker
* xUnit + Moq + FluentAssertions

---

## 🚀 Features

### 🔐 Authentication

* Register
* Login
* JWT Token
* Claims & Roles

### 📁 Projects

* Create Project
* Get All (Paginated)
* Get By Id
* Update
* Delete (Admin Only)

### 📌 Tasks

* Create Task
* Get Tasks By Project
* Update Status
* Delete Task

---

## 🔒 Security & Authorization

### 🔐 Authentication

* JWT Bearer Token
* Secure Identity handling

### 🛡️ Authorization

| Endpoint              | Role        | Rule           |
| --------------------- | ----------- | -------------- |
| GET /projects         | Admin, User | Admin sees all |
| GET /projects/{id}    | Admin, User | Owner or Admin |
| POST /projects        | User        | Any user       |
| PUT /projects/{id}    | User        | Owner or Admin |
| DELETE /projects/{id} | Admin       | Admin only     |
| GET tasks             | Admin, User | Owner or Admin |
| POST task             | User        | Owner only     |
| PATCH status          | User        | Owner only     |
| DELETE task           | Admin, User | Owner or Admin |

---

## ⚡ Caching Strategy (Redis)

### Pattern: Cache-Aside

### Keys Used:

* `projects_user_{userId}`
* `projects_all`
* `project_{id}`
* `tasks_project_{projectId}`

### Behavior:

* Read → check cache first
* Miss → fetch from DB → store in cache
* Write → invalidate cache

---

## 🔄 API Versioning

```
/api/v1/...
```

---

## 🧪 Swagger Testing Guide

### 🔹 Open Swagger

```
https://localhost:7150/swagger
```

---

### 🔹 Authentication Steps

1. POST `/api/v1/auth/login`

```json
{
  "email": "user@pm.com",
  "password": "User123!"
}
```

2. Copy token

3. Click **Authorize**

```
Bearer <token>
```

---

## 🧪 Sample API Requests & Responses

---

### ✅ Create Project

**POST /api/v1/projects**

```json
{
  "name": "Build House",
  "description": "This is a good House"
}
```

**Response**

```json
{
  "succeeded": true,
  "message": "Project created successfully.",
  "data": {
    "id": "80a968fd-2320-495f-b6e0-9da8102fe2b1",
    "name": "Build House",
    "description": "This is a good House",
    "createdAt": "2026-05-23T12:48:16Z",
    "taskCount": 0
  }
}
```

---

### ✅ Get All Projects (Paginated)

**GET /api/v1/projects?pageNumber=1&pageSize=10**

```json
{
  "succeeded": true,
  "data": {
    "items": [
      {
        "name": "Build Villa",
        "taskCount": 1
      }
    ],
    "pageNumber": 1,
    "totalPages": 1
  }
}
```

---

### ✅ Create Task

**POST /api/v1/projects/{projectId}/tasks**

```json
{
  "title": "Build floors",
  "description": "Construction task",
  "priority": 1,
  "dueDate": "2026-05-26T17:37:00Z"
}
```

---

### ✅ Update Task Status

**PATCH /api/v1/projects/{projectId}/tasks/{taskId}/status**

```json
{
  "status": 2
}
```

---

### ✅ Delete Task

**DELETE /api/v1/projects/{projectId}/tasks/{taskId}**

```json
{
  "succeeded": true,
  "data": "Task deleted successfully."
}
```

---

## ⚠️ Common Errors

| Error | Reason              |
| ----- | ------------------- |
| 401   | Missing token       |
| 403   | Unauthorized access |
| 400   | Validation failed   |
| 404   | Resource not found  |

---

## 🐳 Running the Project

### 🔹 Local

```bash
dotnet run --project src/ProjectManagement.API
```

---

### 🔹 Docker

```bash
docker-compose up --build
```

### Access:

* API → [http://localhost:8080](http://localhost:8080)
* Swagger → [http://localhost:8080/swagger](http://localhost:8080/swagger)

---

## 👤 Seeded Accounts

| Role  | Email                               | Password  |
| ----- | ----------------------------------- | --------- |
| Admin | [admin@pm.com](mailto:admin@pm.com) | Admin123! |
| User  | [user@pm.com](mailto:user@pm.com)   | User123!  |

---

## 📊 Logging (Serilog)

* Console + File logs
* Rolling daily logs
* Includes:

  * UserId
  * Request Name
  * Execution Time
  * Exceptions

---

## 🧪 Testing

* 50 Unit Tests
* 100% Pass Rate

### Covered:

* Authentication
* Projects
* Tasks
* Validation
* Caching
* Authorization

---

## 🧠 Design Decisions

* Clean Architecture separation
* CQRS for scalability
* Redis for performance
* RBAC for security
* Global exception handling
* Structured logging

---

## 🏁 Conclusion

This project demonstrates:

* Enterprise backend architecture
* Production-ready API design
* Secure and scalable system
* High performance using caching
* Fully tested and maintainable codebase

---

## 👨‍💻 Author

**Lotfy Abdalla Mosalam**
Full Stack .NET & Angular Developer
