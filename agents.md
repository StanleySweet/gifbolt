# Agent Guidelines for GifBolt Development

This document provides guidance for LLM agents and human contributors
working on the GifBolt project.

## Code Standards Reference

For detailed code standards, refer to [.llm](.llm) - this is the
authoritative source for all code conventions.

## C# Development Standards

### Critical Rules (MUST be followed)

1. **Mandatory Doxygen Documentation**
   - Every public type, method, property, and event requires XML documentation comments
   - Document parameters and return values
   - Include remarks for complex behavior

2. **Explicit `this` Keyword**
   - Always use `this._field`, `this.Property`, `this.Method()`
   - Required for fields, properties, methods, and events
   - This improves code clarity and maintainability

3. **`sealed` Classes by Default**
   - Classes must be marked `sealed` unless explicitly designed for inheritance
   - This prevents accidental breaking changes and improves performance
   - Exception: Abstract base classes and interfaces

4. **Naming Conventions**
   - Private fields: `_camelCase` (with underscore prefix)
   - Public members: `PascalCase`
   - Parameters/locals: `camelCase`
   - Interfaces: `IPascalCase`

### Configuration

All these rules are defined in:

- `.editorconfig` - Enforced by IDE and CI/CD
- `.llm` - Detailed documentation with examples
- `.clang-format` and `.clang-tidy` - For C++ code

## When Implementing Features

1. **Read .llm and .editorconfig** before starting
2. **Apply code standards immediately** - Don't plan to fix later
3. **Add Doxygen comments** as you write code
4. **Mark classes as sealed** unless inheritance is needed
5. **Use explicit this** for all member access
6. **Follow naming conventions** from the start

## Project & Repository Management

### .csproj File Standards

- **Keep .csproj files optimized** - Remove all unnecessary elements
- Use SDK-style project format with minimal explicit configuration
- Only include explicit references when required
- Avoid redundant file includes and metadata

### Repository Structure Policy

- **DO NOT create "_new" or temporary folders**
- The repository is version-controlled - use Git for changes
- Refactor files in-place or move them properly with Git
- Use branches for experimental work, not parallel folder structures
- Temporary folders (e.g., `docs_new`, `src_new`) are not acceptable

---

## Code Review Checklist

When reviewing or generating C# code, verify:

- [ ] All public members have Doxygen documentation
- [ ] All field/property access uses explicit `this`
- [ ] Classes are marked `sealed` (unless base class or abstract)
- [ ] Naming follows: `PascalCase` (public), `_camelCase` (private),
  `camelCase` (local/params)
- [ ] Opening braces on new line (Allman style)
- [ ] All control structures have braces
- [ ] .csproj files are clean and optimized
- [ ] No temporary or "_new" folder structures

## IDE Integration

These rules are automatically enforced in:

- **Visual Studio** - Via EditorConfig support
- **Visual Studio Code** - Via C# extension and EditorConfig
- **JetBrains Rider** - Via EditorConfig support

## CI/CD Enforcement

The project includes pre-commit hooks and CI checks that will:

- Validate code formatting
- Check naming conventions
- Ensure Doxygen comments are present
- Prevent violations from being committed

## Commit Message Standards

Follow the standards in [COMMITS.md](COMMITS.md):

- **Format**: `type(scope): subject`
- **Subject**: Max 50 chars, imperative mood, no period
- **Body**: Explain what and why, wrapped at 72 chars
- **Atomic**: One logical change per commit
- **Examples**: `feat(native): Add GPU optimization`, `fix(wpf): Fix null reference`

## Project Structure

```text
GifBolt/
├── src/
│   ├── GifBolt.Core/       # Cross-platform core library
│   ├── GifBolt.Wpf/        # WPF control wrapper
│   └── GifBolt.Native/     # C++ DirectX implementation
├── tests/                  # Unit tests
├── .editorconfig           # Style enforcement
├── .llm                    # LLM guidelines (detailed)
├── agents.md              # This file
└── COMMITS.md             # Commit message guidelines
```

## Questions

For detailed examples and explanations, see:

- Code standards: [.llm](.llm)
- Commit standards: [COMMITS.md](COMMITS.md)
