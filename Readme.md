# Scalable Session Management System

A scalable and high-performance session management system built with **ASP.NET Core**, **Redis**, and **PostgreSQL**.
This system handles secure user authentication, device/session tracking, concurrency control, and provides robust reporting tools.

---

## 🚀 Features

### 🔐 User Authentication

* Secure login system.
* Passwords stored using **AES-256 encryption** (reversible for validation).

### 🧑‍💻 Session Management

* Create, renew, and delete sessions.
* All session data stored in Redis for maximum speed.

### 📱 Max 2 Active Devices Rule

* Enforces a strict **“Maximum 2 active sessions per user”** limit.
* Automatically evicts the **oldest session** when the limit is exceeded.
* Fully atomic using Redis Lua scripts or transactions.

### 📝 Auto-Registration

* Automatically registers new users on login if they don’t already exist.

### 🔒 RBAC (Role-Based Access Control)

* Supports `User` and `Admin` roles.
* Admins can view global session statistics.

### 📊 Session Reporting

* Users: View all active sessions.
* Admins: Access global statistics (active users, session counts, etc.).

### 🌐 HATEOAS Support

* API responses provide helpful navigation links.

### 📘 Swagger Documentation

* Full interactive API docs at `/swagger`.

---

## 🛠 Technologies Used

* **Framework:** ASP.NET Core 8.0
* **Database:** PostgreSQL (user data)
* **Cache:** Redis (session storage, concurrency control)
* **ORM:** Entity Framework Core
* **Testing:** xUnit, Moq
* **Containerization:** Docker & Docker Compose

---

## 📦 Getting Started

### Prerequisites

* Docker Desktop
* .NET 8.0 SDK (optional — only needed if running locally outside Docker)

---

## 🧰 Installation & Run

### 1️⃣ Clone the repository

```bash
git clone https://github.com/abtin81badie/SessionManager
cd SessionManager
```

### 2️⃣ Run with Docker Compose

Starts the API, PostgreSQL, and Redis containers:

```bash
docker-compose up --build
```

### 3️⃣ Access the Application

* **API:** [http://localhost:8080](http://localhost:8080)
* **Swagger UI:** [http://localhost:8080/swagger](http://localhost:8080/swagger)

---

## ⚙️ Configuration

Configuration is managed in `appsettings.json` and environment variables in `docker-compose.yml`.

Key configuration sections:

### 🔑 Connection Strings

* `ConnectionStrings:Redis` – Redis connection URL
* `ConnectionStrings:Postgres` – PostgreSQL connection URL

### 🔐 AES Encryption

```json
"AesSettings": {
  "Key": "Base64Encoded32ByteKeyHere"
}
```

### 🔏 JWT Settings

```json
"JwtSettings": {
  "Secret": "YourJWTSigningSecret"
}
```

---

## 📡 API Endpoints

| Method | Endpoint              | Description                                         | Auth Required |
| ------ | --------------------- | --------------------------------------------------- | ------------- |
| POST   | `/api/auth/login`     | Authenticate user & create session (auto-register). | ❌ No          |
| DELETE | `/api/auth/logout`    | Invalidate current session.                         | ✔️ Yes        |
| POST   | `/api/sessions/renew` | Renew the current session TTL.                      | ✔️ Yes        |
| GET    | `/api/sessions`       | Get active sessions for current user.               | ✔️ Yes        |
| GET    | `/api/admin/stats`    | Get session stats (user stats or global for admin). | ✔️ Yes        |

---

## 🧪 Testing

Unit tests cover controllers, services, and repositories.

Run tests with:

```bash
dotnet test
```

---

## 📁 Project Structure

```
SessionManager/
├── SessionManager.Api/             # API Controllers & Startup Configuration
├── SessionManager.Application/     # Business Logic, DTOs, Interfaces
├── SessionManager.Domain/          # Domain Entities
├── SessionManager.Infrastructure/  # EF Core, Redis, PostgreSQL, Repositories
└── SessionManager.Tests/           # Unit Tests (xUnit + Moq)
```
