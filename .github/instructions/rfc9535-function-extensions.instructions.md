---
title: "RFC 9535 JSONPath – Function Extensions"
description: >
  Detailed reference for the function extension mechanism defined in RFC 9535
  Section 2.4: type system, well-typedness rules, type conversion, and the five
  built-in functions (length, count, match, search, value).
source: "https://www.rfc-editor.org/rfc/rfc9535#section-2.4"
globs:
  - "**/*.cs"
---

# RFC 9535 – Function Extensions

## 1 Overview

Function extensions are an **extension point** for filter expression
functionality. They are invoked inside filter selectors. Every function:

- Has a **registered name** (`[a-z][_a-z0-9]*`).
- Takes zero or more **typed parameters**.
- Produces a **typed result**.
- Is **free of side effects** — evaluation order does not matter.

```abnf
function-name      = function-name-first *function-name-char
function-name-first = LCALPHA           ; a-z
function-name-char  = LCALPHA / "_" / DIGIT

function-expr      = function-name "(" S [function-argument
                        *(S "," S function-argument)] S ")"
function-argument  = literal /
                     filter-query /     ; includes singular-query
                     logical-expr /
                     function-expr
```

## 2 Type System

| Type | Instances |
|---|---|
| `ValueType` | JSON values **or** the special result `Nothing` |
| `LogicalType` | `LogicalTrue` or `LogicalFalse` |
| `NodesType` | Nodelists (zero or more nodes) |

Key notes:

- `Nothing` represents the **absence** of a JSON value; it is distinct from
  `null`.
- `LogicalTrue` / `LogicalFalse` are **unrelated** to JSON `true` / `false`.
- Only primitive JSON values can be directly expressed as literals in JSONPath.

## 3 Type Conversion

### 3.1 `NodesType` → `LogicalType`

When a `NodesType` expression is used where `LogicalType` is expected:

- **Non-empty** nodelist → `LogicalTrue`
- **Empty** nodelist → `LogicalFalse`

This mirrors existence testing for queries.

### 3.2 No Implicit `NodesType` → `ValueType`

There is **no implicit** conversion from `NodesType` to `ValueType`. Use the
`value()` function to explicitly extract a value from a single-node nodelist.

## 4 Well-Typedness Rules

A function expression is **well-typed** when both conditions hold:

### 4.1 Result Type Matches Context

| Context | Required Declared Result Type |
|---|---|
| As a `test-expr` in a logical expression | `LogicalType` or `NodesType` |
| As a `comparable` in a comparison | `ValueType` |
| As a `function-argument` | Must match the declared parameter type (or be convertible) |

### 4.2 Arguments Match Parameter Types

Each argument must satisfy one of:

| Parameter Type | Valid Arguments |
|---|---|
| `ValueType` | A literal, a singular query, or a function expression with result type `ValueType` |
| `LogicalType` | A `logical-expr` (not a function expression), a function expression with result type `LogicalType`, or a function expression / query with result type `NodesType` (converted) |
| `NodesType` | A query (including singular query), or a function expression with result type `NodesType` |

**Singular query as `ValueType`:** If the query yields one node, the value is
used. If it yields zero nodes, the argument is `Nothing`.

## 5 Built-in Functions

### 5.1 `length()`

| | |
|---|---|
| **Parameters** | 1 × `ValueType` |
| **Result** | `ValueType` (unsigned integer or `Nothing`) |

| Argument Type | Result |
|---|---|
| String | Number of Unicode scalar values |
| Array | Number of elements |
| Object | Number of members |
| Anything else | `Nothing` |

**Example:** `$[?length(@.authors) >= 5]`

### 5.2 `count()`

| | |
|---|---|
| **Parameters** | 1 × `NodesType` |
| **Result** | `ValueType` (unsigned integer) |

Returns the number of nodes in the nodelist. No deduplication. `count(@)` on a
non-empty singular nodelist always returns `1`.

**Example:** `$[?count(@.*.author) >= 5]`

### 5.3 `match()`

| | |
|---|---|
| **Parameters** | 2 × `ValueType` (string, I-Regexp string) |
| **Result** | `LogicalType` |

Tests whether the **entirety** of the first string matches the I-Regexp
(RFC 9485) in the second string.

- If either argument is not a string (or the regex is invalid per RFC 9485):
  `LogicalFalse`.
- Otherwise: `LogicalTrue` if the full string matches; `LogicalFalse` otherwise.

**Example:** `$[?match(@.date, "1974-05-..")]`

### 5.4 `search()`

| | |
|---|---|
| **Parameters** | 2 × `ValueType` (string, I-Regexp string) |
| **Result** | `LogicalType` |

Tests whether the first string **contains a substring** that matches the
I-Regexp (RFC 9485).

- If either argument is not a string (or the regex is invalid): `LogicalFalse`.
- Otherwise: `LogicalTrue` if any substring matches; `LogicalFalse` otherwise.

**Example:** `$[?search(@.author, "[BR]ob")]`

> **`match` vs `search`:** `match` requires the **entire** string to match;
> `search` checks for any matching **substring**.

### 5.5 `value()`

| | |
|---|---|
| **Parameters** | 1 × `NodesType` |
| **Result** | `ValueType` |

Converts a nodelist to a value:

- **One node** → the node's value.
- **Empty or multiple nodes** → `Nothing`.

**Example:** `$[?value(@..color) == "red"]`

> A singular query already produces a `ValueType` in comparisons, so `value()`
> is only needed for non-singular queries.

## 6 Well-Typedness Examples

| Query | Well-Typed? | Reason |
|---|---|---|
| `$[?length(@) < 3]` | ✅ | `@` is singular → `ValueType`; `length` returns `ValueType` in comparison |
| `$[?length(@.*) < 3]` | ❌ | `@.*` is non-singular (not `ValueType` for `length` param) |
| `$[?count(@.*) == 1]` | ✅ | `@.*` is a query → `NodesType` for `count` param |
| `$[?count(1) == 1]` | ❌ | `1` is not a query or function expression |
| `$[?match(@.timezone, 'Europe/.*')]` | ✅ | Both args can be `ValueType` |
| `$[?match(@.timezone, 'Europe/.*') == true]` | ❌ | `match` returns `LogicalType`, cannot be used in comparison |
| `$[?value(@..color) == "red"]` | ✅ | `value` returns `ValueType` for comparison |
| `$[?value(@..color)]` | ❌ | `ValueType` cannot be used in a test expression |

## 7 IANA Function Registry

| Name | Parameters | Result | Description |
|---|---|---|---|
| `length` | `ValueType` | `ValueType` | Length of string/array/object |
| `count` | `NodesType` | `ValueType` | Size of nodelist |
| `match` | `ValueType`, `ValueType` | `LogicalType` | Full regex match |
| `search` | `ValueType`, `ValueType` | `LogicalType` | Substring regex match |
| `value` | `NodesType` | `ValueType` | Extract value from nodelist |

Additional functions may be registered via the IANA "Function Extensions"
subregistry (Expert Review policy). Function names: `[a-z][_a-z0-9]*`.
