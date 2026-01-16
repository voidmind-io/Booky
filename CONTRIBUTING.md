# Contributing to Booky

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>: <description>

[optional body]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Code style (formatting, no logic change) |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build process, dependencies, CI |

### Examples

```
feat: add auto-update check on startup
fix: logout not clearing cookies from memory
docs: add SmartScreen note to README
chore: update workflow permissions
```

### Guidelines

- Keep the description short (50 chars or less)
- Use imperative mood ("add" not "added")
- No period at the end
- Body is optional - use for explaining "why" not "what"

## Pull Requests

1. Fork the repo
2. Create a feature branch (`git checkout -b feat/my-feature`)
3. Commit your changes using conventional commits
4. Push and open a PR

## Development

See [README.md](README.md#building-from-source) for build instructions.
