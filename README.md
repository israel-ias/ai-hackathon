# Habit Builder Helper

A personalized 7-day habit building application powered by AI, providing Christian-based guidance with Bible verses and inspirational quotes.

## Features

- ? **AI-Powered Personalization**: Dynamic questions based on your chosen habit
- ?? **7-Day Micro-Habit Plans**: Actionable daily tasks under 10 minutes
- ?? **Bible Integration**: Relevant verses with full text from KJV translation
- ?? **Inspirational Quotes**: Daily motivation from famous personalities
- ?? **PDF Export**: Download your plan as a beautiful PDF document
- ?? **Modern UI**: Responsive Vue.js frontend with elegant design

## Prerequisites

- .NET 9 SDK
- GitHub Models API access (for AI-powered content generation)

## Setup Instructions

### 1. Clone the Repository
```bash
git clone <your-repository-url>
cd AIHackathon
```

### 2. Configure API Token

**Important**: Never commit your API tokens to version control!

Create a file named `appsettings.Development.json` in the project root with your GitHub Models API token:

```json
{
  "GitHubModels": {
    "ApiToken": "your_github_pat_token_here"
  }
}
```

**Note**: This file is already included in `.gitignore` to prevent accidental commits.

### 3. Alternative Configuration Methods

You can also set the API token using:

**Environment Variable:**
```bash
export GitHubModels__ApiToken="your_github_pat_token_here"
```

**User Secrets (recommended for development):**
```bash
dotnet user-secrets init
dotnet user-secrets set "GitHubModels:ApiToken" "your_github_pat_token_here"
```

### 4. Build and Run

```bash
dotnet build
dotnet run
```

The application will be available at `https://localhost:7xxx` (port displayed in console).

## Configuration Options

### GitHub Models API
- `GitHubModels:ApiToken` - Your GitHub Personal Access Token (required)
- `GitHubModels:DefaultModel` - AI model to use (default: "xai/grok-3")
- `GitHubModels:ApiUrl` - GitHub Models API endpoint
- `GitHubModels:ApiVersion` - API version (default: "2022-11-28")

### External APIs
- `ExternalApis:QuoteApi` - Quotable.io API endpoint
- `ExternalApis:BibleApi` - Bible API endpoint

## Security Best Practices

1. **Never commit secrets**: The `.gitignore` file excludes configuration files with sensitive data
2. **Use User Secrets**: For local development, prefer `dotnet user-secrets`
3. **Environment Variables**: Use environment variables in production
4. **Rotate Tokens**: Regularly rotate your API tokens

## Project Structure

```
AIHackathon/
??? Program.cs                 # Main application and API endpoints
??? appsettings.json          # Public configuration (no secrets)
??? appsettings.Development.json # Development secrets (gitignored)
??? wwwroot/
?   ??? index.html            # Vue.js frontend
?   ??? app.js                # Application logic
??? README.md                 # This file
```

## API Endpoints

- `GET /questions/{habit}` - Get personalized onboarding questions
- `POST /plan` - Generate 7-day habit plan based on answers
- `GET /` - Serve the Vue.js frontend

## Technologies Used

- **Backend**: ASP.NET Core 9 Minimal APIs
- **Frontend**: Vue.js 3, Bootstrap 5, Font Awesome
- **AI**: GitHub Models API (xAI Grok-3)
- **External APIs**: 
  - Bible API (bible-api.com)
  - Quotable API (quotable.io)
- **PDF Generation**: jsPDF

## Troubleshooting

### API Token Issues
- Ensure your GitHub PAT has the necessary permissions
- Verify the token is correctly set in configuration
- Check that the token hasn't expired

### Certificate Errors
- The application bypasses SSL validation in development
- For production, ensure proper SSL certificates are configured

### CORS Issues
- The application allows all origins in development
- Configure appropriate CORS policies for production

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Ensure all secrets are properly configured
5. Submit a pull request

## License

This project is licensed under the MIT License.