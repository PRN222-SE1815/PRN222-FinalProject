# School Management System (PRN222 Final Project)

A comprehensive School Management System built with .NET 8, designed to handle academic administration, student enrollment, gradebook management, and real-time communication.

## 🚀 Technology Stack

- **Framework**: .NET 8.0 (ASP.NET Core Razor Pages)
- **Database**: SQL Server with Entity Framework Core (Database First)
- **Real-time**: SignalR for Hub-based messaging and notifications
- **External APIs**: 
  - **MoMo**: Integrated for tuition and fee payments.
  - **Google Gemini**: AI-powered chat assistant for educational support.
- **Frontend**: Vanilla CSS, JavaScript, and Razor View Components.
- **Architecture**: Clean 3-layer architecture (`BusinessLogic`, `BusinessObject`, `DataAccess`, `Presentation`).

## ✨ Key Features

### 👤 User Roles & Management
- **Admin**: System configuration, user management, and high-level analytics.
- **Teacher**: Class management, attendance, gradebook entry, and scheduling.
- **Student**: Enrollment, course tracking, grade viewing, and tuition payment.

### 📚 Academic Management
- **Curriculum**: Programs, Semesters, Courses, and Prerequisites.
- **Enrollment**: Course registration windows, add/drop deadlines, and capacity management.
- **Gradebook**: Comprehensive grading system with weighted totals, audit logs, and export to CSV/XLSX.
- **Appeals**: Formal grade appeal workflow for students and teachers.

### 🗓️ Scheduling & Calendar
- Real-time class schedules with recurrence support.
- Event overrides for rescheduling or cancellations.
- Timezone-aware event management (`Asia/Ho_Chi_Minh`).

### 💬 Communication & Notifications
- **Real-time Chat**: Course-based and class-based chat rooms with SignalR.
- **File Sharing**: Support for attachments in messages (up to 20MB).
- **Notifications**: Instant updates for schedule changes, grade publications, and mentions.

### 🤖 AI Assistant
- Integrated AI Chat using Google Gemini for tutoring and general academic assistance.

## 🏗️ Project Architecture

The solution follows a structured approach to ensure separation of concerns:
- **`Presentation`**: ASP.NET Core Razor Pages project containing UI, SignalR hubs, and middlewares.
- **`BusinessLogic`**: Services, interfaces, and core business rules.
- **`DataAccess`**: Repository implementations and Entity Framework `DbContext`.
- **`BusinessObject`**: Domain entities, DTOs, and Enums.

## 🛠️ Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [VS Code](https://code.visualstudio.com/)

### Setup Steps
1.  **Clone the repository**:
    ```bash
    git clone https://github.com/[your-repo]/PRN222-FinalProject.git
    ```
2.  **Database Configuration**:
    - Open the `PRN222_G5_finalproject.sql` script in SQL Server Management Studio (SSMS).
    - Execute the script to create the `SchoolManagementDb` and seed initial data.
3.  **App Settings**:
    - Update the connection string in `Presentation/appsettings.json`:
      ```json
      "ConnectionStrings": {
        "DefaultConnection": "Server=YOUR_SERVER;Database=SchoolManagementDb;Trusted_Connection=True;TrustServerCertificate=True"
      }
      ```
    - (Optional) Configure `MoMoSettings` and `GeminiSettings` for full functionality.
4.  **Run the Application**:
    - Open `FinalProject.slnx` in Visual Studio.
    - Set the `Presentation` project as the Startup Project.
    - Press `F5` or run `dotnet run --project Presentation`.

## 📄 License
This project is part of the PRN222 course requirements and is for educational purposes.
