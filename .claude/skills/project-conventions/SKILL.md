---
name: project-conventions
description: Enforces project-specific coding conventions and standards. Use when writing or reviewing code to ensure consistency.
allowed-tools: Read, Grep, Glob
---

# Project Conventions

This skill helps maintain consistency across the codebase by enforcing project-specific conventions and standards.

## When to Use

- Writing new code or features
- Reviewing code changes
- Refactoring existing code
- Setting up new files or modules
- Onboarding to the project

## Code Style Guidelines

### General Principles
- Write clear, self-documenting code
- Prefer readability over cleverness
- Keep functions focused and small (under 50 lines)
- Use meaningful variable and function names
- Avoid deep nesting (max 3-4 levels)

### Naming Conventions
- **Variables**: Use descriptive names (`user_count` not `uc`)
- **Functions**: Use verb phrases (`calculate_total`, `fetch_user_data`)
- **Classes**: Use noun phrases in PascalCase
- **Constants**: Use UPPERCASE_WITH_UNDERSCORES
- **Private members**: Prefix with underscore (`_internal_method`)

### Code Organization
- Group related functions together
- Place imports at the top of files
- Separate concerns into different modules
- Keep configuration separate from logic

## File Organization

```
project/
├── src/              # Source code
├── tests/            # Test files
├── docs/             # Documentation
├── scripts/          # Utility scripts
└── config/           # Configuration files
```

## Documentation Standards

### Code Comments
- Explain *why*, not *what* the code does
- Document complex algorithms or business logic
- Add TODOs with context: `# TODO(username): Fix edge case for...`

## Error Handling

- Catch specific exceptions, not bare `except:`
- Raise meaningful error messages
- Include context in exception messages

## Testing Conventions

- One test file per source file
- Use descriptive test names
- Follow Arrange-Act-Assert pattern
- Cover edge cases and error conditions

## Security Best Practices

- Use environment variables for credentials
- Add `.env` to `.gitignore`
- Validate all user input
- Don't log passwords or tokens

## Git Commit Conventions

### Types
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Test additions or changes
- `chore`: Build process or tool changes

### Example
```
feat: add user authentication endpoint

Implement JWT-based authentication for API access.

Closes #123
```
