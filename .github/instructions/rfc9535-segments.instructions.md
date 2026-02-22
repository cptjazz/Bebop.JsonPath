---
title: "RFC 9535 JSONPath – Segments (Child & Descendant)"
description: >
  Detailed reference for child segments and descendant segments as defined in
  RFC 9535 Section 2.5. Covers bracket notation, dot-notation shorthands,
  evaluation semantics, and normative examples.
source: "https://www.rfc-editor.org/rfc/rfc9535#section-2.5"
globs:
  - "**/*.cs"
---

# RFC 9535 – Segments

## 1 Overview

Segments are the building blocks between the root identifier `$` and the final
result. Each segment takes an input nodelist and produces an output nodelist.

```abnf
segment = child-segment / descendant-segment
```

**Depth rule:**
- A query with **N** segments (N ≥ 0) produces nodes at depth **N or greater**.
- If all N segments are child segments, the result is at **exactly depth N**.

---

## 2 Child Segment

### 2.1 Syntax

```abnf
child-segment       = bracketed-selection /
                      ("."
                       (wildcard-selector /
                        member-name-shorthand))

bracketed-selection = "[" S selector *(S "," S selector) S "]"

member-name-shorthand = name-first *name-char
name-first          = ALPHA / "_" / %x80-D7FF / %xE000-10FFFF
name-char           = name-first / DIGIT
```

### 2.2 Shorthands

| Shorthand | Equivalent Bracket Form |
|---|---|
| `.*` | `[*]` |
| `.foo` | `['foo']` |
| `.foo_bar` | `['foo_bar']` |
| `.名前` | `['名前']` (Unicode names allowed) |

> Dot notation is limited to member names matching `name-first *name-char`.
> Names with spaces, dots, quotes, etc. **must** use bracket notation.

### 2.3 Semantics

For each node in the input nodelist:

1. Apply **every selector** in the segment (in order).
2. **Concatenate** per-selector nodelists (preserving order).
3. A node matched by multiple selectors appears **multiple times**.

Per-input-node results are concatenated in input-nodelist order.

Where a selector can produce results in more than one order (e.g., wildcard on
objects), **each occurrence** may independently produce a different order.

**In summary:** a child segment drills down **one level** into the structure.

### 2.4 Examples

**JSON:**
```json
["a", "b", "c", "d", "e", "f", "g"]
```

| Query | Result | Comment |
|---|---|---|
| `$[0, 3]` | `"a"`, `"d"` | Two index selectors |
| `$[0:2, 5]` | `"a"`, `"b"`, `"f"` | Slice + index combined |
| `$[0, 0]` | `"a"`, `"a"` | Duplicate entries preserved |

---

## 3 Descendant Segment

### 3.1 Syntax

```abnf
descendant-segment = ".." (bracketed-selection /
                           wildcard-selector /
                           member-name-shorthand)
```

### 3.2 Shorthands

| Shorthand | Equivalent |
|---|---|
| `..*` | `..[*]` |
| `..foo` | `..['foo']` |

> `..` alone is **not** a valid segment.

### 3.3 Semantics

For each node in the input nodelist, a descendant segment:

1. **Visits** the input node and all its descendants, where:
   - Array children are visited in array order.
   - Nodes are visited **before** their descendants.
   - Object children order is not stipulated.
2. Let the visited nodes in order be `D1, D2, …, Dn` (D1 = input node).
3. For each `Di`, apply the equivalent child segment `[<selectors>]` to produce
   nodelist `Ri`.
4. The result is the concatenation `R1 ++ R2 ++ … ++ Rn`.

These per-input-node results are then concatenated in input-nodelist order.

**In summary:** a descendant segment drills down **one or more levels**.

### 3.4 Examples

**JSON:**
```json
{
  "o": {"j": 1, "k": 2},
  "a": [5, 3, [{"j": 4}, {"k": 6}]]
}
```

| Query | Result | Comment |
|---|---|---|
| `$..j` | `1`, `4` | All `j` member values (order may vary for objects) |
| `$..[0]` | `5`, `{"j": 4}` | Index 0 at all levels |
| `$..[*]` / `$..*` | all values & nested values | Full descent |
| `$..o` | `{"j": 1, "k": 2}` | Input value itself is visited |
| `$.o..[*, *]` | `1`, `2`, `2`, `1` | Non-deterministic; wildcard repeated |
| `$.a..[0, 1]` | `5`, `3`, `{"j": 4}`, `{"k": 6}` | Selectors applied per visited node |

### 3.5 Ordering Guarantees for `$..[*]`

Given the JSON above, the only guarantees are:

- A parent appears **before** its children.
- Array elements preserve their relative order.
- Object member values may appear in **any** order relative to each other.

Specific guaranteed orderings from the example:

- `{"j": 1, "k": 2}` before `1` and `2`
- `[5, 3, [...]]` before `5`, `3`, and `[{"j":4}, {"k":6}]`
- `5` before `3` before `[{"j":4}, {"k":6}]`
- `{"j": 4}` before `{"k": 6}`
- `{"j": 4}` before `4`; `{"k": 6}` before `6`

## 4 Implementation Notes

- The child segment `[<selectors>]` applies selectors per visited node for
  descendant segments — not once globally. This is critical for correct
  `$.a..[0, 1]` behavior.
- Each occurrence of a wildcard selector in the same segment may independently
  choose a different order for object children.
- Combining multiple selector types in one bracket notation (e.g., `[0:2, 5]`)
  is valid and results are concatenated per-selector.
