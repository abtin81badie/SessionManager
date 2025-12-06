---

# Scalable Session Management System

A scalable and high-performance session management system built with **ASP.NET Core**, **Redis**, and **PostgreSQL**. This system handles secure user authentication, device/session tracking, concurrency control, and provides robust reporting tools.

---

## 🚀 Features

### 🔐 User Authentication & Security

* **JWT Authentication:** Secure stateless authentication using Bearer tokens.
* **AES-256 Encryption:** Passwords and sensitive data stored using reversible AES-256 encryption.
* **Auto-Registration:** Automatically registers new users on login if they don’t already exist.

### ⚙️ Dynamic Environment Configuration

* **Environment Aware:** Automatically detects if running Locally (Windows) or in Docker.
* **Port Management:** Intelligent switching between database ports (Local: `5433` vs Docker: `5432`).

### 🧑‍💻 Session Management

* **Redis Backed:** All session data stored in Redis for high-speed access.
* **Max 2 Devices Rule:** Enforces a strict “Maximum 2 active sessions per user” limit.
* **Automatic Eviction:** Atomic Lua scripts remove the oldest session when the limit is exceeded.

### 📊 Reporting & RBAC

* **Role-Based Access:** Supports `User` and `Admin` roles.
* **HATEOAS Support:** API responses include helpful navigation links.
* **Swagger Documentation:** Full interactive API docs served at the root.

---

## 🛠 Technologies Used

* **Framework:** ASP.NET Core 8.0
* **Database:** PostgreSQL
* **Cache:** Redis
* **ORM:** Entity Framework Core
* **Testing:** xUnit, Moq
* **Containerization:** Docker & Docker Compose

---

## 📦 Getting Started

### Prerequisites

* Docker Desktop
* .NET 8.0 SDK *(optional — only needed if running locally outside Docker)*
* Git

---

## 🧰 Installation & Run

### 1️⃣ Clone the repository

```bash
git clone https://github.com/abtin81badie/SessionManager
cd SessionManager
```

### 2️⃣ Configure Environment Variables (`.env`)

Create a `.env` file in the **root directory** (next to `.sln` and `docker-compose.yml`):

```ini
# --- DATABASE ---
DB_PASSWORD=LocalSecurePassword123!

# --- SECURITY KEYS ---
JWT_SECRET=ThisIsASecretKeyForJwtTokenGenerationMustBeLongEnough
JWT_ISSUER=SessionManager
JWT_AUDIENCE=SessionManagerClient
JWT_EXPIRY=60
AES_KEY=MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=

# --- ADMIN USER ---
ADMIN_USERNAME=admin
ADMIN_PASSWORD=MySuperSecretAdminPass123!

# --- SESSION SETTINGS ---
SESSION_MAX_CONCURRENT=2
SESSION_TIMEOUT_MINUTES=60
```

### 3️⃣ Run with Docker Compose

Starts the API, PostgreSQL, and Redis containers:

```bash
docker-compose up --build
```

---

## 🌐 Access the Application

The application serves **Swagger UI at `/index.html`**.

| Environment         | URL                                                                                | Description                          |
| ------------------- | ---------------------------------------------------------------------------------- | ------------------------------------ |
| **☁️ Live Server**  | **[http://103.75.198.136:8080/index.html](http://103.75.198.136:8080/index.html)** | Public deployed instance             |
| **🐳 Docker Local** | **[http://localhost:8080/index.html](http://localhost:8080/index.html)**           | Running inside Docker                |
| **💻 Dev Local**    | **[https://localhost:7206/index.html](https://localhost:7206/index.html)**         | Running via Visual Studio / .NET CLI |

---

## ⚙️ Configuration Details

### Database Connection Logic

* **Docker Mode:** Uses host `postgres` on port **5432**
* **Local Mode:** Uses host `localhost` on port **5433**
  (mapped in `docker-compose.yml` to avoid conflicts with local PostgreSQL)

### Key Settings

| Key                       | Description            |
| ------------------------- | ---------------------- |
| `ConnectionStrings:Redis` | Redis connection URL   |
| `JwtSettings:Secret`      | JWT signing key        |
| `AesSettings:Key`         | AES-256 encryption key |

---

## 📡 API Endpoints

| Method     | Endpoint              | Description                        | Auth   |
| ---------- | --------------------- | ---------------------------------- | ------ |
| **POST**   | `/api/auth/login`     | Authenticate user & create session | ❌ No   |
| **DELETE** | `/api/auth/logout`    | Invalidate current session         | ✔️ Yes |
| **POST**   | `/api/sessions/renew` | Renew session TTL                  | ✔️ Yes |
| **GET**    | `/api/sessions`       | Get user’s active sessions         | ✔️ Yes |
| **GET**    | `/api/admin/stats`    | Global stats (Admin only)          | ✔️ Yes |

---

## 🧪 Testing

Run unit tests:

```bash
dotnet test
```

---

## 📁 Project Structure

```
SessionManager/
├── SessionManager.Api/           # Entry point
│   ├── Controllers/              # Thin controllers (Map Request -> Command)
│   └── Extensions/               # Service registration & Startup logic
│
├── SessionManager.Application/   # Core Business Logic
│   ├── Behaviors/                # MediatR Validation Pipeline
│   ├── DTOs/                     # Data Transfer Objects
│   ├── Features/                 # CQRS (Commands, Queries, Handlers)
│   └── Interfaces/               # Abstractions (ICurrentUserService, Repos)
│
├── SessionManager.Infrastructure/ # Implementation Details
│   ├── Persistence/              # EF Core & Redis implementations
│   ├── Services/                 # Crypto, Token, & User Services
│   └── Options/                  # Configuration settings
│
├── SessionManager.Domain/        # Domain Entities
└── docker-compose.yml            # Container Orchestration
```

---