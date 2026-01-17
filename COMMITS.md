# GifBolt Commit Message Guidelines

This guide explains how to write clear, atomic, and maintainable commits when contributing to GifBolt, based on best practices from [cbea.ms/git-commit](https://cbea.ms/git-commit).

## Quick Summary

- Separate subject from body with a blank line
- Limit the subject line to 50 characters
- Capitalize the subject line
- Do not end the subject line with a period
- Use the imperative mood in the subject line
- Wrap the body at 72 characters
- Use the body to explain what and why vs. how
- Write atomic, logically separated commits
- Use imperative, present tense in commit summaries
- Link relevant issues with `Refs: #1234` or `Fixes: #1234`

## Commit Message Format

### Example

```
Add workaround to turn off nursery size heuristic

SpiderMonkey 98 introduced a size heuristic for the nursery GC region
(https://phabricator.services.mozilla.com/D136637). As this heuristic
uses a wall-clock time duration, it results in a severe performance
regression on slower systems for our use case.

This commit adds a workaround to turn off that heuristic, by telling
SpiderMonkey that a "page load" (something which doesn't have a meaning
in the context of pyrogenesis) is in progress, as that heuristic is
disabled for page loads.

Fixes: #7714
```

### Subject Line (First Line)

**Format:**
```
<type>(<scope>): <subject>
```

**Rules:**
1. **Type** - One of: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `ci`, `build`
2. **Scope** - Component affected (e.g., `native`, `wpf`, `core`, `tests`, `ci`)
3. **Subject** - Imperative, present tense, no period, max 50 characters
   - ✅ "Add renderer caching"
   - ❌ "Added renderer caching"
   - ❌ "Adds renderer caching"
   - ❌ "Add renderer caching."

**Examples:**
```
feat(native): Add GPU-accelerated frame decoding
fix(wpf): Fix null reference in GifBoltControl
docs(readme): Update installation instructions
style(cs): Fix naming convention violations
refactor(core): Extract decoder logic to separate class
perf(native): Optimize frame buffer allocation
test(renderer): Add tests for frame timing
chore(deps): Update giflib to 5.2.1
ci(github): Add coverage report to CI
build(cmake): Add Windows build support
```

### Body (Optional, but recommended)

**Rules:**
1. Separated from subject by blank line
2. Wrapped at 72 characters
3. Explain **what** and **why**, not **how**
4. Use present tense
5. Use bullet points for multiple changes

**Example:**
```
Fix null reference exception in frame buffer allocation

When the video dimensions exceed maximum texture size, the frame
buffer allocation would fail with a null reference exception instead
of gracefully degrading or throwing a proper exception.

This commit:
- Add size validation before buffer allocation
- Throw ArgumentException with meaningful message if size is invalid
- Add integration tests for edge cases

Fixes: #1234
```

## Commit Granularity

Each commit should ideally represent a **single logical change**. This helps reviewers understand and test specific functionality, and makes future debugging and code archaeology significantly easier.

### Good Commit Examples:

```
fix(wpf): Fix null reference in GifBoltControl
```

```
refactor(native): Extract decoder to separate file
```

```
perf(core): Cache decoded frames for repeated playback
```

### Bad Commit Examples:

```
Fix formation bug and tweak GUI
```

```
Various fixes and improvements
```

```
WIP: lots of stuff
```

## Guidelines for GifBolt

### When to Create a New Commit

✅ **DO create separate commits for:**
- Bug fixes
- New features
- Performance improvements
- Refactoring unrelated code
- Documentation updates
- Style/formatting fixes
- Test additions

❌ **DON'T mix in one commit:**
- Bug fix + unrelated refactoring
- Feature + style changes
- Multiple unrelated features

### Component Scopes

Use these scopes for GifBolt commits:

| Scope | Description |
|-------|-------------|
| `native` | C++ DirectX renderer code |
| `core` | C# core library (GifBolt.Core) |
| `wpf` | C# WPF control (GifBolt.Wpf) |
| `tests` | Test code and test infrastructure |
| `ci` | CI/CD pipelines and GitHub Actions |
| `build` | Build configuration (CMake, csproj) |
| `docs` | Documentation files |
| `chore` | Dependencies, version bumps |

### Commit Types

| Type | Purpose | Example |
|------|---------|---------|
| `feat` | New feature | "Add async frame loading" |
| `fix` | Bug fix | "Fix memory leak in decoder" |
| `docs` | Documentation | "Update API documentation" |
| `style` | Code style (naming, formatting) | "Fix naming conventions" |
| `refactor` | Code restructuring (no behavior change) | "Extract decoder interface" |
| `perf` | Performance improvement | "Optimize frame buffer allocation" |
| `test` | Test additions/updates | "Add decoder edge case tests" |
| `chore` | Dependencies, build config | "Update CMake version requirement" |
| `ci` | CI/CD changes | "Add code coverage reporting" |
| `build` | Build system changes | "Add Windows CI pipeline" |

## Linking Issues

Use these keywords to link commits to issues:

```
Fixes: #1234          # Closes the issue
Refs: #1234           # References the issue (doesn't close)
Closes: #1234         # Closes the issue
Resolves: #1234       # Closes the issue
Related to: #1234     # Related but doesn't close
```

**Example:**
```
fix(native): Fix frame timing calculation

The frame duration was being incorrectly calculated when the GIF
had variable frame delays. This commit recalculates based on the
actual GIF frame timing data.

Fixes: #542
```

## Pre-commit Checklist

Before committing, verify:

- [ ] Subject line is 50 characters or less
- [ ] Subject starts with lowercase type and scope: `type(scope):`
- [ ] Subject uses imperative mood ("Add" not "Added")
- [ ] Subject has no period at the end
- [ ] Body is wrapped at 72 characters
- [ ] Body explains what and why, not how
- [ ] Each commit represents a single logical change
- [ ] No multiple unrelated changes in one commit
- [ ] Issue references use correct keywords (Fixes, Refs, etc.)
- [ ] Code passes linting and tests before committing

## Examples of Good Commits

### Feature
```
feat(native): Add frame caching for repeated playback

Cache decoded frames in memory to avoid re-decoding the same frame
when playing the same GIF multiple times. This significantly improves
playback performance on low-end systems.

- Add LRU cache for up to 100 decoded frames
- Add cache statistics for performance monitoring
- Add configuration option to disable caching

Refs: #156
```

### Bug Fix
```
fix(wpf): Fix null reference exception on unload

When GifBoltControl was unloaded before the native renderer finished
initialization, accessing the handle in OnUnloaded would throw a
NullReferenceException.

Add null check before accessing native handle in cleanup methods.

Fixes: #234
```

### Refactoring
```
refactor(core): Extract GIF metadata parsing

Move GIF metadata parsing from GifDecoder to separate GifMetadataParser
class to improve code organization and testability.

- Extract parsing logic to GifMetadataParser
- Update GifDecoder to use new class
- Add unit tests for metadata parsing

No behavioral changes.
```

### Performance
```
perf(native): Reduce frame buffer allocations

Reuse frame buffer across frames instead of allocating a new buffer
for each frame. This reduces memory fragmentation and improves
performance on systems with limited memory.

Benchmarks show 15% improvement in memory usage for 1000-frame GIFs.
```

---

## References

- [Conventional Commits](https://www.conventionalcommits.org/)
- [How to Write a Git Commit Message](https://cbea.ms/git-commit/)
- [The Art of Commit Messages](https://www.freecodecamp.org/news/how-to-write-better-git-commit-messages/)
