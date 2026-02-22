---
title: "RFC 9535 JSONPath – Filter Expressions"
description: >
  Detailed reference for the filter selector (Section 2.3.5 of RFC 9535),
  covering logical expressions, existence tests, comparisons, operator
  precedence, and singular queries.
source: "https://www.rfc-editor.org/rfc/rfc9535#section-2.3.5"
globs:
  - "**/*.cs"
---

# RFC 9535 – Filter Expressions

## 1 Overview

The **filter selector** `?<logical-expr>` iterates over children of structured
values (arrays and objects). For each child, the logical expression is
evaluated; the child is selected when the expression is logically true.

- Applied to a **primitive** value → selects nothing.
- Array children appear in array order.
- Object children order is **not stipulated**.

The **current node identifier** `@` refers to the node being tested in the
immediately enclosing filter selector. Nested filters each have their own `@`;
there is no syntax to reach an outer `@`.

## 2 Syntax

```abnf
filter-selector   = "?" S logical-expr

logical-expr      = logical-or-expr
logical-or-expr   = logical-and-expr *(S "||" S logical-and-expr)
logical-and-expr  = basic-expr *(S "&&" S basic-expr)

basic-expr        = paren-expr /
                    comparison-expr /
                    test-expr

paren-expr        = [logical-not-op S] "(" S logical-expr S ")"
logical-not-op    = "!"

test-expr         = [logical-not-op S]
                    (filter-query /   ; existence test
                     function-expr)   ; LogicalType or NodesType

filter-query      = rel-query / jsonpath-query
rel-query         = current-node-identifier segments
current-node-identifier = "@"
```

### 2.1 Operator Precedence (high → low)

| Precedence | Operator | Syntax |
|---|---|---|
| 5 | Grouping, Functions | `(...)`, `name(...)` |
| 4 | Logical NOT | `!` |
| 3 | Relations | `==` `!=` `<` `<=` `>` `>=` |
| 2 | Logical AND | `&&` |
| 1 | Logical OR | `||` |

Parentheses **MAY** be used for grouping but are not required around the
top-level expression (unlike some older JSONPath proposals).

Evaluation order is **not defined** (no side effects); both short-circuit and
full evaluation are valid.

## 3 Existence Tests

A **query by itself** (without a comparison operator) in a logical context is
an existence test:

- **true** if the query selects at least one node.
- **false** if the query selects zero nodes.

Key distinctions:

- Works with arbitrary queries (not just singular queries).
- Works with structured values.
- To test a specific **value**, use an explicit comparison:
  - `@.foo == null` tests whether `@.foo` exists **and** its value is `null`.
  - `!@.foo` is true when `@.foo` selects **no** node, regardless of value.
  - `@.foo == false` is true only when the node exists and has the value `false`.

## 4 Comparisons

### 4.1 Syntax

```abnf
comparison-expr = comparable S comparison-op S comparable

comparable      = literal /
                  singular-query /   ; value of the single selected node
                  function-expr      ; ValueType

comparison-op   = "==" / "!=" /
                  "<=" / ">=" /
                  "<"  / ">"
```

### 4.2 Singular Queries

Only **singular queries** (guaranteed to produce at most one node) may appear
in comparisons.

```abnf
singular-query          = rel-singular-query / abs-singular-query
rel-singular-query      = current-node-identifier singular-query-segments
abs-singular-query      = root-identifier singular-query-segments
singular-query-segments = *(S (name-segment / index-segment))
name-segment            = ("[" name-selector "]") /
                          ("." member-name-shorthand)
index-segment           = "[" index-selector "]"
```

When a singular query produces a one-node nodelist, the node's value is used.
When it produces an empty nodelist, the result is treated like `Nothing` (see
below).

### 4.3 Literals

```abnf
literal = number / string-literal / true / false / null
number  = (int / "-0") [ frac ] [ exp ]
```

`true`, `false`, `null` are **case-sensitive** lowercase.

### 4.4 Equality (`==`)

When **either side** is an empty nodelist or `Nothing`:
- `==` is **true** only if the other side is also empty / `Nothing`.

Otherwise (both sides have values):
- **Numbers**: equal per mathematical equality (I-JSON interoperable numbers).
- **Strings / booleans / null**: equal if identical primitives.
- **Arrays**: same length and each element pair-wise equal.
- **Objects** (no duplicate names): same set of names, and for each name the
  associated values are equal.
- **Cross-type**: always **false** (e.g., `13 == '13'` → false).

### 4.5 Less-Than (`<`)

When either side is empty / `Nothing`: always **false**.

Otherwise, `<` is defined **only** between:
- Two **numbers**: normal mathematical ordering (I-JSON).
- Two **strings**: Unicode scalar value lexicographic ordering.

For any other type combination (object, array, boolean, null, cross-type):
`<` yields **false**.

### 4.6 Derived Operators

| Operator | Definition |
|---|---|
| `a != b` | `!(a == b)` |
| `a <= b` | `a < b \|\| a == b` |
| `a > b`  | `b < a` |
| `a >= b` | `b < a \|\| a == b` |

## 5 Comparison Truth Table (reference)

**JSON:**
```json
{
  "obj": {"x": "y"},
  "arr": [2, 3]
}
```

| Comparison | Result | Comment |
|---|---|---|
| `$.absent1 == $.absent2` | true | Empty nodelists |
| `$.absent1 <= $.absent2` | true | `==` implies `<=` |
| `$.absent == 'g'` | false | Empty vs. value |
| `$.absent1 != $.absent2` | false | Empty nodelists |
| `$.absent != 'g'` | true | Empty vs. value |
| `1 <= 2` | true | Numeric |
| `1 > 2` | false | Numeric |
| `13 == '13'` | false | Type mismatch |
| `'a' <= 'b'` | true | String |
| `'a' > 'b'` | false | String |
| `$.obj == $.arr` | false | Type mismatch |
| `$.obj != $.arr` | true | Type mismatch |
| `$.obj == $.obj` | true | Object equality |
| `$.arr == $.arr` | true | Array equality |
| `$.obj == 17` | false | Type mismatch |
| `$.obj <= $.arr` | false | No `<` for objects/arrays |
| `true <= true` | true | `==` implies `<=` |
| `true > true` | false | No `<` for booleans |

## 6 Filter Examples

**JSON:**
```json
{
  "a": [3, 5, 1, 2, 4, 6,
        {"b": "j"},
        {"b": "k"},
        {"b": {}},
        {"b": "kilo"}],
  "o": {"p": 1, "q": 2, "r": 3, "s": 5, "t": {"u": 6}},
  "e": "f"
}
```

| Query | Result | Comment |
|---|---|---|
| `$.a[?@.b == 'kilo']` | `{"b": "kilo"}` | Member value comparison |
| `$.a[?(@.b == 'kilo')]` | `{"b": "kilo"}` | Parenthesized (equivalent) |
| `$.a[?@>3.5]` | `5`, `4`, `6` | Array value comparison |
| `$.a[?@.b]` | `{"b":"j"}`, `{"b":"k"}`, `{"b":{}}`, `{"b":"kilo"}` | Existence test |
| `$[?@.*]` | `[3,5,…]`, `{"p":1,…}` | Non-singular existence |
| `$[?@[?@.b]]` | `[3,5,…]` | Nested filters |
| `$.a[?@<2 \|\| @.b == "k"]` | `1`, `{"b":"k"}` | Logical OR |
| `$.o[?@>1 && @<4]` | `2`, `3` | Logical AND |
| `$.o[?@.u \|\| @.x]` | `{"u": 6}` | Existence OR |
| `$.a[?@.b == $.x]` | `3`,`5`,`1`,`2`,`4`,`6` | Both sides empty → equal |
| `$.a[?@ == @]` | all 10 elements | Self-equality |

## 7 Implementation Notes

- Filter selectors work **exclusively** with arrays and objects; primitives
  produce empty results.
- The logical type used internally (`LogicalTrue` / `LogicalFalse`) is
  **distinct** from JSON `true` / `false`.
- When evaluating `&&` or `||`, short-circuit and full evaluation are both
  valid (no side effects).
- Object children may appear in **non-deterministic** order.
