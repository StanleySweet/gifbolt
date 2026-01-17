# Agent Guidelines for GifBolt Development

This document provides guidance for LLM agents and human contributors working on the GifBolt project.

## Code Standards Reference

### Documentation

For complete and authoritative code standards, refer to [.llm](.llm)

The `.llm` file contains comprehensive documentation covering:

- C# code standards (naming, documentation, `this` keyword, `sealed` classes)
- C++ code standards (naming, documentation, memory management, RAII)
- Project file management (.csproj optimization)
- Repository structure policies
- Code review checklists

## Quick Reference

### Critical Rules

1. **Read [.llm](.llm) before starting any work**
2. **Apply code standards immediately** - Don't plan to fix later
3. **All public members require XML documentation**
4. **Use explicit `this` for all member access**
5. **Mark classes as `sealed` unless inheritance is needed**
6. **NO "_new" or temporary folder structures**

### Configuration Files

- [.llm](.llm) - **Primary source of truth** for all code standards
- `.editorconfig` - Enforces C# naming and style
- `.clang-format` - C++ formatting rules
- `.clang-tidy` - C++ static analysis
- [COMMITS.md](COMMITS.md) - Commit message standards

### Commit Message Standards

Follow the standards in [COMMITS.md](COMMITS.md):

- **Format**: `type(scope): subject`
- **Subject**: Max 50 chars, imperative mood, no period
- **Body**: Explain what and why, wrapped at 72 chars
- **Atomic**: One logical change per commit

## Project Structure

```text
GifBolt/
├── src/
│   ├── GifBolt.Core/       # Cross-platform core library
│   ├── GifBolt.Wpf/        # WPF control wrapper
│   └── GifBolt.Native/     # C++ DirectX implementation
├── tests/                  # Unit tests
├── .llm                    # PRIMARY CODE STANDARDS (read this!)
├── .editorconfig           # Style enforcement
├── agents.md              # This file (quick reference)
└── COMMITS.md             # Commit message guidelines
```

## IDE Integration

These rules are automatically enforced in:

- **Visual Studio** - Via EditorConfig support
- **Visual Studio Code** - Via C# extension and EditorConfig
- **JetBrains Rider** - Via EditorConfig support

## For Complete Documentation

See [.llm](.llm) for:

- Detailed examples and explanations
- Complete naming conventions
- Documentation requirements
- Memory management standards
- Project file optimization rules
- Code review checklists
