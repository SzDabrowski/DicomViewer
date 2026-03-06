# provisioner-koApoK

Claude Code configuration project for managing development workflows.

## Project Type
Type: Unknown
Frameworks: None detected

## Project Structure
```
agent_data/
├── agents/              # Sub-agent definitions
├── skills/              # Reusable skill modules
└── settings.json        # Claude Code configuration
```

## Available Sub-Agents

### code-reviewer
Expert code review specialist that analyzes code for:
- Code quality and readability
- Security vulnerabilities
- Best practices compliance
- Test coverage
- Performance considerations

**Usage**: Automatically invoked after writing or modifying code.

## Available Skills

### project-conventions
Enforces coding standards and conventions:
- Code style guidelines
- File organization patterns
- Documentation standards
- Testing conventions
- Security best practices

## Development Workflow

1. **Write Code**: Implement features or fixes
2. **Auto Review**: Code-reviewer agent analyzes changes
3. **Address Feedback**: Fix critical issues and warnings
4. **Commit**: Follow conventional commit format

## Coding Standards

- Write clear, self-documenting code
- Keep functions small and focused (under 50 lines)
- Use meaningful names for variables and functions
- Document complex logic and business rules
- Always handle errors appropriately
- Never commit secrets or credentials

## Security Guidelines

- Use environment variables for sensitive data
- Validate and sanitize all inputs
- Implement proper error handling
- Follow principle of least privilege

## Getting Started

Claude Code will automatically:
- Load project conventions skill
- Invoke code-reviewer after code changes
- Enforce security and quality standards
