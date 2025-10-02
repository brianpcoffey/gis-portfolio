Here’s a professional **README** for your portfolio project based on the details you’ve shared. It’s structured, clean, and suitable for GitHub or personal documentation:

---

# Brian Patrick Coffey – Software Engineering Portfolio

## Overview

This is a portfolio web application showcasing the work and expertise of **Brian Patrick Coffey**, a software engineer focused on creating innovative solutions for the **United States Air Force**. The portfolio demonstrates projects across **financial systems, administrative automation, and geographic information systems (GIS)**, highlighting modern web development skills, secure authentication, and responsive design.

Built with **ASP.NET Core 8**, **Razor Pages**, and **Bootstrap 5**, the portfolio includes **Google authentication integration**, **dark/light mode toggle**, and **responsive layouts** suitable for both desktop and mobile devices.

---

## Features

### Authentication

* **Google OAuth 2.0 Sign-In**

  * Secure login using Google accounts
  * Automatically retrieves basic profile information (name, email, profile picture)
  * Forced login modal ensures users authenticate before accessing content
* **Cookie-based session management**
* **Sign-out functionality** with seamless redirection

### UI & UX

* **Responsive design** with Bootstrap 5
* **Dark/Light mode toggle** with localStorage persistence
* **Sleek profile avatar dropdown**

  * No extra dropdown arrows
  * Compact logout menu
* **Card-based project showcase**

  * Icons, titles, and descriptions for each project
* **About Me section**

  * Professional summary with card-like styling
  * Fully readable in light and dark mode

### Projects Highlighted

* **Geographic Information Systems (GIS)**

  * Mini-projects leveraging GIS software for Air Force operational analysis
* **Financial Systems**

  * Secure, efficient applications for budgeting, reporting, and resource management
* **Administrative Automation**

  * Tools to automate routine administrative tasks, improving productivity

### Additional

* Google Sign-In button styled similar to native Google UI
* Accessible and semantic markup
* Fully mobile-responsive

---

## Technology Stack

* **Backend:** ASP.NET Core 8, Razor Pages
* **Frontend:** Bootstrap 5, Font Awesome, Vanilla JavaScript
* **Authentication:** Google OAuth 2.0, Cookie Authentication
* **Styling:** Custom CSS with dark/light mode support
* **Documentation:** Swagger/OpenAPI integration

---

## Getting Started

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* A Google Developer Console project with **OAuth 2.0 credentials**
* IDE or code editor (Visual Studio, VS Code, etc.)

### Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/username/portfolio.git
   ```
2. Navigate to the project folder:

   ```bash
   cd portfolio
   ```
3. Add your **Google OAuth Client ID** and **Client Secret** to `appsettings.json`:

   ```json
   "Authentication": {
       "Google": {
           "ClientId": "YOUR_CLIENT_ID",
           "ClientSecret": "YOUR_CLIENT_SECRET"
       }
   }
   ```
4. Restore dependencies and run the application:

   ```bash
   dotnet restore
   dotnet run
   ```
5. Open your browser at `https://localhost:5001` to view the portfolio.

---

## License

This project is public. Feel free to use or adapt it for personal portfolios.
