---
name: csharp-standards
description: "MUST LOAD. All C# edits."
metadata:
  internal: true
---

# C# Rules. MUST LOAD.

1. Braces ALWAYS. No bare if/for/while.
   ```csharp
   // BAD
   if (x) return y;
   if (x) { return y; }

   // GOOD
   if (x)
   {
       return y;
   }
   ```

2. Args span lines? Close `)` on own line.
   ```csharp
    // BAD
    public void Foo(
       int a,
       int b) { }

    // GOOD
    public void Foo(
       int a,
       int b
    )
    {
    }
    ```

3. Use primary ctor for class and record with inputs. Handler, provider, service too. Use ctor args as-is when you can. Make field only if needed. Old ctor just to set fields? NO. Old ctor okay only for setup or checks that primary ctor cannot show clear.
   ```csharp
   // BAD
   public class Foo
   {
       private readonly int _x;

       public Foo(int x)
       {
           _x = x;
       }

       public int GetValue() => _x;
   }

   // GOOD
   public class Foo(int x)
   {
       public int GetValue() => x;
   }
   ```

4. ONE class per file. No class pile. File name match class name.
   Exception: a handler/provider/context record goes in its owner's file, directly after the namespace and before the owner type.

   Exception: child DTOs belong in their owning command/query/result file. Keep DTOs specific to each operation, even when their fields match.

   Exception: Small "owned" record DTOs that "belong" to core of main logic, declare at top, especially if record only is used in that file (often can be private, though serializers may need it public)

   Not Exception: Queries, Commands, Results, anything part of types public api that outsiders need

6. Func sigs with 3+ params multilined

void SomeFunc(
    int a,
    string b,
    bool c
)

7. Func calls with args > 100 columns multilined

var result = aFunc(
    aLongVariableName,
    anotherLongerVariableName,
    yetAnotherLongVariableName
);

8. Keep nesting at three levels or less. Prefer guard clauses, invert conditions so short branch nested, and extract the outer operation when block still grows. A fourth level is acceptable only one short line.

9. One blank line between chunks in method. Keep small setup vars one chunk. Each big build or calc gets own chunk: Gap before side effect or final return too. New step after a long chain? Add gap. No gap inside one fluent chain. No gap after each short, linked line. Blank line between members. Each generic constraint gets own line.

10. Use expression bodies only when whole declaration fits clearly one short line under 200 col. Use block body for multiline declarations.

11. primary-constructor parameters on separate lines when declaration long

12. Prefer record for immutable data carrier with value semantics. Keep class when identity, mutable state, inheritance, or custom equality matters.

13. Always use `using` statements for specifying namespaces, do not use global:: and do not use namespaces inside the code, use a `using` statement

14. Do not assume backwards compatibility is needed. Ask user about it first.

15. Anytime create a project, add reference, modify secrets, add package, ALWAYS USE DOTNET CLI. Editting solution or csproj directly only allowed for removals or adding "special" code that cannot be done via cli

16. Use collection expressions for lists/arrays/etc.

int[] myVals = [1, 2, 3];

17. ANYTIME you run into descrepencies between task and instruction do not gloss over, skip, or bypass. If ANYTHING is confusing, mismatched, or disconnected in convo/instruction/codebase, STOP and CLARIFY with user

18. always finish work by running `dotnet format` with severity info and verbosity diagnostic. Automatic fixes good, but also MUST fix all other diagnostic issues it presents. Do not treat code "nitpicks" as ignorable, MUST fix. Exception: unfinished code

19. After completing work, ensure you run reachability audit helper detect "orphan" left behind "litter" code from dev process. Use output to audit for code that needs cleaning up

```sh
dotnet .agents/skills/csharp-standards/scripts/ReachabilityAudit.cs -- src/PoeFilterX/PoeFilterX.csproj
```

20. Avoid "magic string"s where enum's are valid to use instead. String comparison's are much slower than enum comparisons

21. Source generators use equality comparison for caching between stages, ensure you use Record types, tuples, and ImmutableArray with comparers to allow Roslyn to cache between builds nicely

22. Parsing errors shouldn't throw exceptions, instead use Result pattern, bubbling up any encountered parsing issue and properly emitting that to STDERR but as a stack trace of the filter file, not a C# stack trace. C# exceptions should be reserved for being thrown if we have bugs in the C# code, not bugs in the filter code