# Brian Patrick Coffey – Software Engineering Portfolio

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?style=flat&logo=bootstrap&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Neon.tech-4169E1?style=flat&logo=postgresql&logoColor=white)
![Hosted on Render](https://img.shields.io/badge/Hosted_on-Render-46E3B7?style=flat&logo=render&logoColor=white)

## Overview

A modern software engineering portfolio showcasing projects in geographic information systems (GIS), smart home data applications, and interactive state-level tools. Built with an emphasis on secure authentication, responsive design, and cloud deployment.

The portfolio is built with **ASP.NET Core 10**, **Razor Pages**, and **Bootstrap 5**, featuring Google OAuth authentication, dark/light mode toggle, and layouts optimized for desktop and mobile. It is hosted on **Render**, with a **Neon.tech PostgreSQL** database powering project data and backend functionality.

---

## Features

### Authentication
- **Google OAuth 2.0 Sign-In** — secure login using Google accounts
- Retrieves basic profile information (name, email, profile picture)
- Login modal ensures authentication before accessing content
- Cookie-based session management
- Sign-out functionality with smooth redirection

### UI & UX
- Fully responsive design with Bootstrap 5
- Dark/Light mode toggle with preference persistence
- Profile avatar dropdown with compact logout menu
- Card-based project showcase with icons, titles, and descriptions
- About Me section with professional summary and dark/light mode styling

---

## Projects Highlighted

### 🗺️ US State Explorer
An interactive state-level data exploration tool using GIS mapping and visualization techniques. PostgreSQL (Neon.tech) backend stores state and demographic data.

### 🏠 Redlands Smart Home Finder
An application for locating and comparing smart home devices in Redlands. Filters and sorts devices based on features and pricing, connected to a Neon.tech PostgreSQL database for real-time inventory.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# |
| Frontend | Bootstrap 5, Font Awesome, Vanilla JavaScript, Razor Pages |
| Database | PostgreSQL (Neon.tech) |
| Authentication | Google OAuth 2.0, Cookie Authentication |
| Styling | Custom CSS with dark/light mode support |
| Containerization | Docker |
| Hosting | Render |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Google Developer Console](https://console.developers.google.com/) project with OAuth 2.0 credentials
- Access to a [Neon.tech](https://neon.tech/) PostgreSQL database
- IDE or code editor (Visual Studio, VS Code, etc.)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/username/portfolio.git
   ```

2. **Navigate to the project folder:**
   ```bash
   cd portfolio
   ```

3. **Add your credentials to `appsettings.json`:**
   ```json
   "Authentication": {
       "Google": {
           "ClientId": "YOUR_CLIENT_ID",
           "ClientSecret": "YOUR_CLIENT_SECRET"
       }
   },
   "Database": {
       "ConnectionString": "Host=your_neon_host;Port=5432;Database=your_db;Username=your_user;Password=your_password;"
   }
   ```

4. **Restore dependencies and run the application:**
   ```bash
   dotnet restore
   dotnet run
   ```

5. Open your browser at `https://localhost:5001` (or your Render URL) to view the portfolio.

---

## Deployment

- Hosted on **Render** with continuous deployment from GitHub.
- Database hosted on **Neon.tech**, ensuring cloud scalability and performance.

---

## License

This project is public. You may use or adapt it for personal portfolios or learning purposes.
