# ExamDynamicsAPI

ExamDynamicsAPI is a **scalable, secure, and modular ASP.NET Core Web API** designed to manage **examination systems** such as exams, subjects, questions, students, teachers, results, and authentication. The project follows **clean architecture principles** and is suitable for **academic projects, final-year submissions, and real-world learning**.

---

## 📌 Project Overview

The purpose of **ExamDynamicsAPI** is to provide a backend system that supports:

* Exam creation and management
* Subject & question bank handling
* Student registration and participation
* Secure authentication using JWT
* Result calculation and reporting

This API can be integrated with **Angular, React, or any frontend framework**.

---

## 🧠 Key Features

* ✅ RESTful API using ASP.NET Core
* 🔐 JWT-based Authentication & Authorization
* 🧩 Clean Architecture / Layered Structure
* 🗄️ SQL Server with Entity Framework Core (Code First)
* 👤 Role-based Access (Admin, Teacher, Student)
* 📊 Exam Results & Performance Tracking
* 📄 Swagger API Documentation

---

## 🏗️ Architecture Used

This project follows a **Clean / Onion Architecture** approach:

---

ExamDynamicsAPI
│
├── ExamDynamics.API          → Controllers & Middleware
├── ExamDynamics.Application  → DTOs, Interfaces, Business Logic
├── ExamDynamics.Domain       → Entities & Enums
├── ExamDynamics.Infrastructure → EF Core, Database, Services

---

### Why This Architecture?

* Loose coupling
* Easy testing & maintenance
* Separation of concerns
* Industry-level best practice

---

## 🛠️ Technologies & Tools

* **Backend:** ASP.NET Core Web API (.NET 8)
* **ORM:** Entity Framework Core (Code First)
* **Database:** Microsoft SQL Server
* **Authentication:** JWT (JSON Web Tokens)
* **Documentation:** Swagger / OpenAPI
* **IDE:** Visual Studio / VS Code

---

## 🔐 Authentication & Security

The API uses **JWT Authentication** to secure endpoints.

### Authentication Flow

1. User registers or logs in
2. Server generates a JWT token
3. Token is sent in the Authorization Header
4. Protected endpoints validate the token

---

Authorization: Bearer <your_token_here>

---

Roles supported:

* **Admin** – Full access
* **Teacher** – Exam & Question Management
* **Student** – Attempt Exams & View Results

---

## 📂 Database Design (Conceptual)

Main entities include:

* User
* Role
* Subject
* Exam
* Question
* Options
* StudentExam
* Result

Relationships are managed using **EF Core navigation properties**.

---

## 🚀 Getting Started

### Prerequisites

* .NET SDK 8.0+
* SQL Server
* Visual Studio / VS Code

### Clone the Repository

---

git clone https://github.com/your-username/ExamDynamicsAPI.git
cd ExamDynamicsAPI

---

---

## ⚙️ Configuration

Update the `appsettings.json` file:

* Database connection string
* JWT Secret Key
* Token expiration settings

---

"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=ExamDynamicsDB;Trusted_Connection=True;"
}

---

## 🧪 Database Migration

Run the following commands:

---

Add-Migration InitialCreate
Update-Database

---

This will create the database schema automatically.

---

## ▶️ Running the Project

1. Open the solution in Visual Studio
2. Set **ExamDynamics.API** as startup project
3. Press **Run (F5)**
4. Swagger UI will open at:

---

https://localhost:<port>/swagger

---

## 📘 API Documentation (Swagger)

Swagger provides:

* All available endpoints
* Request/response schemas
* Authentication testing support

You can test secured endpoints by clicking **Authorize** and pasting your JWT token.

---

## 📌 Sample Modules

* Authentication Module
* Exam Management Module
* Question Bank Module
* Result Calculation Module

---

## 🎯 Use Cases

* University / College Examination Systems
* Online Quiz Platforms
* Learning Management Systems (LMS)
* Academic Final Year Projects

---

## 📈 Future Enhancements

* ⏱️ Timed Exams
* 📊 Analytics Dashboard
* 📱 Mobile App Integration
* ☁️ Cloud Deployment (Azure)
* 📧 Email Notifications

---

## 🧑‍🎓 Academic Note

This project is **well-structured and documented**, making it suitable for:

* Semester projects
* Final year projects (FYP)
* Viva & practical exams

---

## 🤝 Contribution

Contributions are welcome!

1. Fork the repository
2. Create a new branch
3. Commit your changes
4. Open a Pull Request

---

## 📜 License

This project is for **educational purposes**.

---

## 👨‍💻 Author

**Ali Ahsan**
Software Developer | .NET & Angular Enthusiast

---

<img width="1920" height="1020" alt="ExamDynamicsUI - Google Chrome 12_27_2025 10_27_27 AM" src="https://github.com/user-attachments/assets/2cee10d6-6da2-4103-9795-6f069dfe89fd" />

⭐ If you find this project helpful, don’t forget to **star the repository**!
