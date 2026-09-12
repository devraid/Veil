# Veil

Veil is an ongoing project aiming to become a Windows desktop AI chat application.

It uses:

- React, TypeScript, and Vite
- C# and .NET 10
- WPF and WebView2
- SQLite and Entity Framework Core
- EF Core migrations for database versioning

## Run the application

Start the frontend:

```powershell
cd frontend
npm install
npm run dev
```

Then open `Veil.slnx` in Visual Studio and press **F5** using the `Veil` launch profile.

The React frontend runs inside the WPF WebView2 window at `http://localhost:5173`.
