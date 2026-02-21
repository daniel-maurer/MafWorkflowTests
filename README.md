# MafWorkflowTests - Multi-Agent Support Workflow System

A sophisticated multi-agent support workflow system built with Microsoft's Agent Framework for automated customer issue triage, problem detection, and resolution.

## Project Overview

This system implements a three-stage AI-powered support workflow:

1. **Triage Agent** - Analyzes and classifies support requests
2. **Frequent Problem Agent** - Identifies known issues from a knowledge base
3. **Resolution Agent** - Attempts automatic resolution using available tools

## Architecture

### Components

- **Agents**: Specialized AI agents for each workflow stage
- **Executors**: Handle agent execution and state management
- **Factories**: Create configured agent instances
- **Models**: Data structures for JSON serialization/deserialization
- **Tools**: Helper functions for agents (e.g., knowledge base lookup)

### Design Patterns

- **Factory Pattern**: AgentFactories for centralized agent creation
- **Executor Pattern**: Encapsulates workflow execution logic
- **State Management**: Uses workflow context for conversation history persistence
- **Dependency Injection**: Constructor-based injection for testability

## Key Features

✅ Multi-agent orchestration with Microsoft.Agents.AI  
✅ Automatic problem classification and urgency assessment  
✅ Known issue detection and resolution suggestion  
✅ Iterative conversation with max iteration limits to prevent infinite loops  
✅ State persistence across workflow stages  
✅ Configuration management via environment variables  
✅ Comprehensive error handling and validation  
✅ XML documentation for public APIs  

## Configuration

### Environment Variables

```bash
AZURE_OPENAI_ENDPOINT=<your-azure-openai-endpoint>
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini  # Optional, defaults to gpt-4o-mini
KNOWN_ISSUES_PATH=know_issues.json        # Optional, defaults to know_issues.json
```

### Required Files

- `know_issues.json` - Knowledge base of known issues (in pt-BR)

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Azure OpenAI API credentials
- Azure CLI authentication configured

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd MafWorkflowTests

# Install dependencies
dotnet restore

# Set up environment variables
cp .env.example .env
# Edit .env with your credentials
```

### Running the Application

```bash
dotnet run
```

The application will:
1. Load configuration from environment variables
2. Initialize the multi-agent workflow
3. Start the support chat interface
4. Process user requests through the triage → detection → resolution pipeline

## Project Structure

```
MafWorkflowTests/
├── Program.cs                          # Entry point with error handling
├── WorkflowFactory.cs                  # Builds the workflow pipeline
├── WorkflowConfiguration.cs            # Configuration management
├── Constants.cs                        # Centralized constants
├── ChatSupport.cs                      # Data model for chat
├── ConsoleInteractor.cs                # Console I/O handler
├── ResolutionExecutor.cs               # Final resolution stage
├── AgentFactories/
│   ├── TriageAgentFactory.cs
│   ├── FrequentProblemAgentFactory.cs
│   └── ResolutionAgentFactory.cs
├── Executors/
│   ├── TriageExecutor.cs
│   └── FrequentProblemExecutor.cs
├── Models/
│   ├── TriageResult.cs
│   ├── FrequentProblemResult.cs
│   └── KnownIssue.cs
├── AiTools/
│   └── FrequentProblemTools.cs
└── know_issues.json                    # Knowledge base
```

## Code Quality Improvements

### Recent Enhancements

- ✅ **Fixed filename typo**: `Cosntants.cs` → `Constants.cs`
- ✅ **Implemented ResolutionExecutor**: Previously empty, now fully functional
- ✅ **Added error handling**: Comprehensive try-catch in Program.cs
- ✅ **Created WorkflowConfiguration**: Centralized configuration management
- ✅ **Fixed infinite loop**: Added MaxWorkflowIterations constant
- ✅ **Added XML documentation**: All public types and methods documented
- ✅ **Input validation**: Null checks on all public methods
- ✅ **State management**: Replaced magic strings with constants
- ✅ **JSON consistency**: Fixed property name casing in KnownIssue

### Best Practices Applied

1. **Configuration Management** - Environment-based configuration with validation
2. **Error Handling** - Structured exception handling with meaningful messages
3. **Documentation** - Comprehensive XML docs for API discoverability
4. **Constants** - Magic strings replaced with named constants
5. **Validation** - Input validation on constructors and public methods
6. **State Management** - Centralized conversation history tracking
7. **Iteration Limits** - Prevents infinite loops with max iteration checks
8. **Async/Await** - Proper async patterns throughout

## Known Issues & Limitations

- Agent instructions are in Portuguese (pt-BR)
- Knowledge base file must be manually maintained
- No database persistence (uses in-memory state)
- Console-based UI only
- No user authentication or multi-user support

## Future Improvements

- [ ] Implement logging abstraction (ILogger)
- [ ] Add unit and integration tests
- [ ] Extract prompts to separate resource files
- [ ] Add database persistence
- [ ] Implement web API interface
- [ ] Add metrics and monitoring
- [ ] Support multiple languages dynamically
- [ ] Implement user session management

## Code Review Summary

See `CODE_REVIEW.md` for detailed analysis of:
- Critical issues and fixes
- Major code quality improvements
- Specific recommendations for each file
- Best practices applied

## License

[Your License Here]

## Support

For issues or questions, please open an issue in the repository.

---

**Last Updated**: 2026-02-20  
**Status**: Ready for first commit
