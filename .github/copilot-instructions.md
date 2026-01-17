# GitHub Copilot Instructions for GifBolt

This file configures GitHub Copilot to follow GifBolt's mandatory code standards.

## C# Code Standards (MANDATORY - ZERO WARNINGS)

### Naming Conventions (ENFORCED)
- **Types** (classes, structs, enums): `PascalCase`
- **Interfaces**: `PascalCase` with `I` prefix (e.g., `IGifDecoder`)
- **Public Members** (properties, methods, fields, events): `PascalCase`
- **Private Fields**: `_camelCase` with leading underscore (e.g., `_nativeHandle`)
- **Local Variables & Parameters**: `camelCase`

### Critical Requirements

1. **Explicit `this` Keyword (ERROR)**
   - MUST use `this._field`, `this.Property`, `this.Method()`
   - Required for fields, properties, methods, and events
   - Example: `this._isLoaded = true;` NOT `_isLoaded = true;`

2. **`sealed` Classes by Default (ERROR)**
   - Classes MUST be marked `sealed` unless explicitly designed for inheritance
   - Exception: Abstract base classes and interfaces
   - Example: `public sealed class GifBoltControl : Control { }`

3. **Doxygen Documentation (MANDATORY)**
   - ALL public types, methods, properties, and events require XML documentation
   - Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags
   - Example:
     ```csharp
     /// <summary>
     /// Loads a GIF from the specified file path.
     /// </summary>
     /// <param name="path">The file path or URI to the GIF image.</param>
     /// <returns>true if successful; otherwise false.</returns>
     /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
     public bool LoadGif(string path) { }
     ```

4. **Brace Style (Allman)**
   - Opening braces on new line for all blocks
   - Use braces for all control structures
   - Example:
     ```csharp
     public bool Load(string path)
     {
         if (string.IsNullOrEmpty(path))
         {
             return false;
         }
         return true;
     }
     ```

5. **No Warnings Policy**
   - ALL naming violations are ERRORS (not warnings)
   - ALL style violations are ERRORS (not warnings)
   - Code must pass `.editorconfig` validation without any errors
   - Every public member must have documentation

---

## C++ Code Standards (MANDATORY - ZERO WARNINGS)

### Naming Conventions
- **Classes/Structs**: `PascalCase`
- **Functions**: `camelCase`
- **Member Variables**: `_camelCase` with leading underscore
- **Constants**: `UPPER_SNAKE_CASE`
- **Local Variables**: `camelCase`

### Critical Requirements

1. **Smart Pointers Required (ERROR)**
   - Use `std::unique_ptr` and `std::shared_ptr` for dynamic allocation
   - NO raw pointers (`T*`) in public APIs
   - Example: `std::unique_ptr<GifDecoder> decoder = std::make_unique<GifDecoder>();`

2. **RAII Pattern (MANDATORY)**
   - All resources must be acquired in constructor, released in destructor
   - Use exception-safe code
   - Example: Use smart pointers, not manual new/delete

3. **Documentation Comments**
   - ALL public classes and functions require documentation
   - Use Doxygen-style C++ comments
   - Document parameters, return values, and exceptions
   - Example:
     ```cpp
     /// \brief Decodes a GIF file from the specified path.
     /// \param filePath The file system path to the GIF image.
     /// \return true if decoding succeeded, false otherwise.
     bool DecodeGif(const std::string& filePath);
     ```

4. **Brace Style (Allman)**
   - Opening braces on new line
   - Use braces for all blocks
   - Example:
     ```cpp
     bool GifRenderer::render(size_t frameIndex)
     {
         if (frameIndex >= _frames.size())
         {
             return false;
         }
         return true;
     }
     ```

5. **Compiler Warnings (ZERO TOLERANCE)**
   - Code MUST compile without warnings using `-Wall -Wextra -Wpedantic -Werror`
   - No signed/unsigned mismatches
   - No shadowed variables
   - No unused variables or parameters
   - No implicit conversions without explicit casts

6. **Static Analysis**
   - Code must pass `.clang-tidy` checks
   - No memory safety violations
   - No resource leaks
   - No null pointer dereferences

---

## Code Review Checklist

Before submitting code, verify:

### C#
- [ ] All public members have Doxygen documentation
- [ ] All field/property access uses explicit `this`
- [ ] Classes are marked `sealed` (unless base class or abstract)
- [ ] Naming follows conventions: `PascalCase` (public), `_camelCase` (private)
- [ ] Braces on new line (Allman style)
- [ ] All control structures have braces
- [ ] No warnings in build output

### C++
- [ ] All public classes/functions are documented
- [ ] All dynamic allocations use smart pointers
- [ ] Follows RAII pattern
- [ ] Braces on new line (Allman style)
- [ ] All control structures have braces
- [ ] Compiles without warnings (`-Wall -Wextra -Wpedantic -Werror`)
- [ ] Passes `.clang-tidy` checks

---

## Key Configuration Files

- `.editorconfig` - Enforces C# naming and style (ERRORS for violations)
- `.clang-format` - C++ formatting rules (Allman style, 100 char limit)
- `.clang-tidy` - C++ static analysis and modernization
- `agents.md` - Detailed LLM agent guidelines
- `.llm` - Extended documentation for LLM agents

---

## Philosophy

**ZERO TOLERANCE POLICY**: All code standards are enforced as ERRORS, not suggestions. There are no warnings in GifBolt code—violations must be fixed immediately. This ensures consistency, readability, and maintainability across the entire codebase.

When writing code, apply standards **immediately**. Don't plan to fix them later. Follow the conventions from the start.
