# Microsoft BASIC for .NET User Manual

This document explains how to use the C# re-implementation of Microsoft BASIC that ships with this repository. It is split into two main sections:

1. **User Guide** – a narrative tutorial that walks through day-to-day usage, editing programs, running games like `STARTREK.BAS`, and using modern conveniences such as `LOAD` and `SAVE`.
2. **Reference Guide** – a catalogue of statements, functions, operators, and runtime behaviour supported by the interpreter, intended as a quick look-up once you know what you want to accomplish.

All examples assume you are running the interpreter via `dotnet run --project src/MicrosoftBasicApp/MicrosoftBasicApp.csproj` (or the corresponding executable). The interpreter behaves very similarly to the classic Microsoft BASIC dialects used on early 8-bit systems, but there are a few modern additions and clarifications discussed below.

---

## 1. User Guide

### 1.1 Getting Started

1. Make sure the .NET 8 SDK is available (`dotnet --version` should report 8.0.x). The repository already pins the SDK through `global.json`.
2. From the repository root, start the interpreter:

   ```bash
   dotnet run --project src/MicrosoftBasicApp/MicrosoftBasicApp.csproj
   ```

3. You should see the banner:

   ```
   MICROSOFT BASIC FOR DOTNET 10
   READY.
   >
   ```

4. The prompt (`>`) accepts immediate commands (`RUN`, `LIST`, `LOAD`, etc.) or numbered program lines.

### 1.2 Entering and Editing Programs

The interpreter stores a program as numbered lines. To add or replace a line, type the line number followed by the statement(s):

```
> 10 PRINT "HELLO, WORLD"
> 20 GOTO 10
```

Key points:

- Re-entering a line number replaces the previous contents. Typing a line number with no code deletes the line.
- One line may contain multiple statements separated by `:`.
- Use the question mark (`?`) as shorthand for `PRINT`, and the apostrophe (`'`) as shorthand for `REM`.
- Variable names are case-insensitive; the interpreter normalises everything internally.

To inspect the stored program, use `LIST` (optionally restrict with line ranges like `LIST 100-200`).

### 1.3 Running Programs

- `RUN` compiles and executes the stored program from the lowest line number, clearing variables beforehand.
- `CONT` is not implemented; execution always starts from the top of the program.
- Errors are reported with a leading `?` message (e.g. `?Undefined line 500`). The program stops and the interpreter returns to `READY.`

Example: loading and running the included Star Trek game.

```
> LOAD "STARTREK.BAS"
READY.
> RUN
```

The game will print its intro, then prompt for `COMMAND?`. Gameplay now follows the original BASIC logic with modern input buffering.

### 1.4 Immediate Commands

At the prompt you can issue interpreter commands (they are not stored in the program):

| Command | Description |
|---------|-------------|
| `RUN` | Execute the current program. |
| `LIST` | Display the program listing. |
| `NEW` | Clear the program. |
| `CLEAR` | Clear variables, arrays, and DATA pointer without touching the program. |
| `LOAD <file>` | Replace the current program with the contents of a `.BAS` file. |
| `SAVE [<file>]` | Save the current program to disk. If no file is supplied, the last `LOAD`/`SAVE` path is reused. |
| `BYE` / `EXIT` / `QUIT` | Leave the interpreter. |

Lines beginning with numbers are treated as program edits even when issued without `NEW`.

### 1.5 LOAD and SAVE Details

- File paths may be absolute or relative; wrap them in double quotes to include spaces. Example: `SAVE "Programs/Startrek.bak"`.
- Within a quoted path, write doubled quotes (`""`) to embed a literal quote character.
- On `SAVE` with no argument, the interpreter uses the most recent path seen in `LOAD` or `SAVE`. If none exists, BASIC reports `?Expected file name`.

### 1.6 Variables and Types

- Numeric variables default to `0` and store double-precision floating point values.
- String variables end with `$` (e.g. `A$`) and default to the empty string.
- Arrays are created automatically with indices `0` through `10` unless explicitly dimensioned with `DIM`. String arrays must end with `$` as well (`A$(10)`), numeric arrays do not (`A(10)`).
- `DIM` arguments specify the maximum index for each dimension; BASIC allocates size `max + 1` internally.

### 1.7 Input and Output Basics

#### PRINT

```
PRINT expression [;|, expression ...]
```

- A trailing `;` suppresses the newline. A trailing `,` advances to the next tab stop (8-column multiples).
- Functions `TAB(n)` and `SPC(n)` produce spacing strings for formatted output.

#### INPUT

```
INPUT ["Prompt";] var[, var ...]
```

- With a prompt string, BASIC prints `Prompt? ` before reading.
- Re-prompts on invalid numeric input.
- Strings are read as raw text; numeric input uses invariant culture (period as decimal separator).

### 1.8 Control Flow Essentials

- `IF`…`THEN` [statement or line] [`ELSE` statement/line]. Multiple statements can follow `THEN` or `ELSE` separated by `:`.
- `FOR var = start TO limit [STEP n]` … `NEXT [var]`. EXIT by falling through or by `GOTO`/`RETURN`.
- `GOTO line`, `GOSUB line`, `RETURN`.
- `STOP` / `END` halt the program (and return to `READY.`).
- `ON expr GOTO line1, line2, ...` (1-based dispatch). Same pattern for `ON ... GOSUB`.

### 1.9 DATA, READ, and RESTORE

- `DATA` statements embed literal values. `READ` pulls the next values sequentially into variables (re-prompting for type mismatch).
- `RESTORE` resets the read pointer to the first `DATA`. `RESTORE line` jumps to the first `DATA` at or after `line`.

### 1.10 RANDOMIZE and RND

- `RANDOMIZE` reseeds the pseudo-random generator using the current environment tick count.
- `RANDOMIZE seed` uses a numeric expression for the seed.
- `RND` without arguments returns a new random double in `[0,1)`. `RND(n)` is recognised; if `n < 0` the generator is reseeded with `abs(n)` before generating a value.

### 1.11 User-Defined Functions

```
DEF FNNAME(parameter, ...) = expression
```

- Functions can return numeric or string values depending on the expression.
- The interpreter tracks defined function names and allows later calls simply as `FNNAME(arg)`.
- Function scope reuses the main variable pool: arguments temporarily shadow existing variables but are restored on return.

### 1.12 File I/O

```
OPEN "path" FOR INPUT|OUTPUT|APPEND AS #channel
PRINT #channel, expression[, ...]
INPUT #channel, var[, ...]
CLOSE [#channel [, #channel ...]]
```

- `INPUT` mode reads comma-separated values, honouring quoted strings; `OUTPUT` truncates the file; `APPEND` writes to the end.
- When printing to files, commas insert literal commas, semicolons concatenate. Adding `;` at the end suppresses the newline.
- `CLOSE` with no arguments closes all open files.
- Files are referenced by numeric channels (integers); you choose the number when calling `OPEN`.

### 1.13 Error Handling

- Runtime errors raise `?message` and exit the program. Common cases: division by zero, `UNDEFINED LINE`, `OUT OF DATA`, `RETURN WITHOUT GOSUB`.
- Input stream exhaustion (CTRL+D / end of file) during `INPUT` results in `?INPUT received end of stream`.

---

## 2. Reference Guide

### 2.1 Immediate Commands Summary

| Command | Arguments | Effect |
|---------|-----------|--------|
| `RUN` | – | Clear variables and execute program. |
| `LIST [start[-end]]` | Optional line range | Show program lines. |
| `NEW` | – | Clear program listing. |
| `CLEAR` | – | Reset variables/arrays/DATA pointer. |
| `LOAD path` | Required | Load program from file. |
| `SAVE [path]` | Optional | Save program to file. |
| `BYE` / `EXIT` / `QUIT` | – | Leave interpreter. |

### 2.2 Statements and Keywords

| Statement | Syntax Highlights | Description |
|-----------|-------------------|-------------|
| `REM` / `'` | `REM comment` | Comment to end of line. |
| Assignment | `LET` optional: `A = expr`, `A(I) = expr`, `A$(I,J) = expr` | Stores expression into variable/array element. |
| `PRINT` | `PRINT [#chn,] item {;|, item} ...` | Writes to console or file. `?` shorthand. |
| `INPUT` | `INPUT [prompt;] var[, var...]` | Reads console input. Add `#chn,` to read from file. |
| `IF` | `IF cond THEN statement-or-line [: ...] [ELSE ...]` | Conditional execution. `THEN` or `ELSE` may point to a line number or contain a colon-separated statement block. |
| `FOR`/`NEXT` | `FOR var = start TO limit [STEP step]` … `NEXT [var]` | Looping construct. Overwrites `var` with start value immediately. |
| `GOTO` | `GOTO line` | Jumps to line number. |
| `GOSUB` / `RETURN` | `GOSUB line` / `RETURN` | Subroutine call/return using an internal stack. |
| `ON ... GOTO` / `GL` | `ON expr GOTO line1, line2, ...` | Jump to nth line based on rounded value of expression. |
| `ON ... GOSUB` | `ON expr GOSUB line1, line2, ...` | Dispatch table for subroutines. |
| `END` | `END` | Terminates program. |
| `STOP` | `STOP` | Same as `END` but semantically “pause”; here it terminates. |
| `CLEAR` | `CLEAR` | Clears variables and arrays. |
| `DIM` | `DIM A(upper[, ...]) [, B(upper[, ...])]` | Allocate arrays with explicit upper bounds (inclusive). Each bound < 0 is clamped to 0. |
| `DATA` | `DATA literal[, literal ...]` | Stores literal values for `READ`. |
| `READ` | `READ var[, var...]` | Pulls values from the `DATA` stream. |
| `RESTORE` | `RESTORE [line]` | Reset `DATA` pointer to start or specified line. |
| `RANDOMIZE` | `RANDOMIZE [seed]` | Seed random generator; `seed` optional numeric expression. |
| `DEF` | `DEF FNNAME(param[, ...]) = expression` | Define a user function. |
| `OPEN` | `OPEN "path" FOR INPUT|OUTPUT|APPEND AS #channel` | Open file for subsequent `PRINT #`/`INPUT #`. |
| `CLOSE` | `CLOSE [#channel [, #channel...]]` | Close specific channels or all if omitted. |
| `RETURN` | `RETURN` | Return from last `GOSUB`. |

Reserved keywords also include logical operators (`AND`, `OR`, `NOT`), the prompt-only commands listed earlier, and built-in function names.

### 2.3 Operators

| Category | Operators | Notes |
|----------|-----------|-------|
| Arithmetic | `+`, `-`, `*`, `/`, `^` | `^` is exponentiation; unary `-` negates; unary `+` allowed. Division is floating-point; division by 0 raises an error. |
| Relational | `=`, `<>`, `<`, `<=`, `>`, `>=` | Work on numbers and strings. String comparisons use ordinal (ASCII) ordering. |
| Logical | `AND`, `OR`, `NOT` | Operate on numeric truth values (non-zero is true). Internally uses bitwise semantics on rounded integers, matching classic BASIC behaviour (`TRUE` is `-1`). |
| Concatenation | `+` between strings | When either operand is a string, result is string concatenation. |

Operator precedence (highest to lowest): unary `+` / `-`, `^`, `*`/`/`, `+`/`-`, relational operators, `AND`, `OR`. Parentheses override precedence.

### 2.4 Built-in Numeric and String Functions

| Function | Arguments | Description |
|----------|-----------|-------------|
| `ABS(x)` | 1 | Absolute value. |
| `ATN(x)` | 1 | Arctangent (radians). |
| `COS(x)` | 1 | Cosine. |
| `EXP(x)` | 1 | Exponential. |
| `INT(x)` | 1 | Floor (largest integer ≤ x). |
| `LOG(x)` | 1 | Natural logarithm. |
| `RND([seed])` | 0 or 1 | Random number in `[0,1)`. If `seed < 0`, reseeds with `abs(seed)`. |
| `SGN(x)` | 1 | Sign: `-1`, `0`, or `1`. |
| `SIN(x)` | 1 | Sine. |
| `SQR(x)` | 1 | Square root. |
| `TAN(x)` | 1 | Tangent. |
| `GET()` | 0 | Reads the next character from standard input and returns its ASCII code (buffered by line). |
| `LEN(s$)` | 1 | String length. |
| `LEFT$(s$, n)` | 2 | Leftmost `n` chars. |
| `RIGHT$(s$, n)` | 2 | Rightmost `n` chars. |
| `MID$(s$, start[, length])` | 2 or 3 | Substring beginning at 1-based `start`. Optional `length`. |
| `CHR$(code)` | 1 | Character from ASCII code. |
| `ASC(s$)` | 1 | ASCII code of first char (0 if empty). |
| `STR$(x)` | 1 | String representation of number (leading space for non-negative values). |
| `VAL(s$)` | 1 | Numeric value parsed from start of string (supports scientific notation, `D` treated as `E`). |
| `TAB(n)` | 1 | Returns string of `n` spaces. Useful inside `PRINT`. |
| `SPC(n)` | 1 | Same as `TAB`. |

### 2.5 Literals

- **Numbers**: Decimal with optional fractional part and exponent (`1`, `3.14`, `1.0E-3`).
- **Strings**: Surrounded by double quotes. Use `""` to represent a single `"` inside the string.
- **Booleans**: No direct literal; use numeric `0` (false) and non-zero (true). Comparisons yield `-1` for true, `0` for false.

### 2.6 Arrays and Data Storage

- Undimensioned array references automatically allocate with upper bound `10` on each dimension.
- `DIM` may only be executed once per array name; re-dimensioning triggers `?Array NAME already dimensioned`.
- Array indices must evaluate to integers in range; otherwise BASIC raises `?Index out of range`.

### 2.7 File System Behaviour

- Paths are interpreted relative to the process working directory.
- `OPEN` automatically closes an existing channel number before reopening it.
- `PRINT #` flushes writer buffers immediately after each call.
- `INPUT #` reads CSV-style tokens (strings may be quoted). End-of-file raises `?End of file on channel n`.

### 2.8 DATA Stream Handling

- `READ` pulls values in order of appearance. Once exhausted, BASIC raises `?Out of data`.
- `RESTORE` with a line number positions the pointer at the first `DATA` whose line number is ≥ the argument. If none exists, reading starts at EOF (leading to `?Out of data` on next `READ`).

### 2.9 Random Number Generation

- Default seed is based on the system clock each time a program is run or `CLEAR` is executed.
- `RANDOMIZE` without arguments reseeds from the current environment tick count, allowing fresh sequences.
- `RND` shares state across the program and functions; there is no separate RNG per `RANDOMIZE` invocation.

### 2.10 Interpreter Limits and Differences from Vintage BASIC

- The interpreter uses modern double-precision math (as opposed to 32-bit single-precision).
- There is no `AUTO`, `DELETE`, or `EDIT` command; editing is line-by-line.
- `CONT` is not implemented; re-run program after any stop.
- `CLEAR` does not take arguments for stack/heap sizes; it simply resets variables, arrays, and random seed.
- Input and output are line-buffered through .NET, so `GET()` reads from the next available character in the buffered console input, appending a newline when you press Enter.

---

## 3. Appendix: Tips for Playing `STARTREK.BAS`

- Use `LOAD "STARTREK.BAS"` to import the program, then `RUN`.
- When prompted with `COMMAND?`, type abbreviations such as `SRS`, `NAV`, `TOR`, `PHA` exactly as shown in the in-game help.
- The interpreter’s updated control flow fixes a historical `IF` parsing quirk; you no longer drop out of the command loop after the initial prompt.
- Save your session after custom modifications with `SAVE "startrek-custom.bas"`.

---

Happy hacking! This guide should give you everything you need to explore the classic BASIC environment while benefiting from the convenience of a modern, cross-platform runtime.

