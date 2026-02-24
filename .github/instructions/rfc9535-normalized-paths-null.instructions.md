---
title: "RFC 9535 JSONPath – Normalized Paths & Null Semantics"
description: >
  Reference for Normalized Paths (Section 2.7) and the semantics of JSON null
  (Section 2.6) as defined in RFC 9535.
source: "https://www.rfc-editor.org/rfc/rfc9535#section-2.6"
globs:
  - "**/*.cs"
---

# RFC 9535 – Normalized Paths & Null Semantics

---

## 1 Normalized Paths

### 1.1 Definition

A **Normalized Path** is a canonical JSONPath expression that **uniquely
identifies** one node in a value.

- Each node has **exactly one** Normalized Path.
- When applied as a query, a Normalized Path produces a one-node nodelist.
- Normalized Paths are **singular queries**, but not all singular queries are
  Normalized Paths (e.g., `$[-3]` is singular but not normalized).

### 1.2 Syntax

```abnf
normalized-path      = root-identifier *(normal-index-segment)
normal-index-segment = "[" normal-selector "]"
normal-selector      = normal-name-selector / normal-index-selector
```

Key rules:

- **Bracket notation only** — no dot notation.
- **Single quotes** for member names (reduces escaping inside double-quoted
  JSON strings).
- **Non-negative** integer indices only.

#### Name Selector

```abnf
normal-name-selector = %x27 *normal-single-quoted %x27   ; 'string'
normal-single-quoted = normal-unescaped /
                       ESC normal-escapable

normal-unescaped     = %x20-26 /    ; omit 0x27 '
                       %x28-5B /    ; omit 0x5C \
                       %x5D-D7FF /
                       %xE000-10FFFF
```

#### Escaped Characters

```abnf
normal-escapable = %x62 /   ; \b  BS  U+0008
                   %x66 /   ; \f  FF  U+000C
                   %x6E /   ; \n  LF  U+000A
                   %x72 /   ; \r  CR  U+000D
                   %x74 /   ; \t  HT  U+0009
                   "'" /    ; \'  '   U+0027
                   "\" /    ; \\  \   U+005C
                   (%x75 normal-hexchar)
```

`normal-hexchar` covers only the non-printable control characters that have no
standard short escape:

```abnf
normal-hexchar = "0" "0"
                 (
                   ("0" %x30-37) /     ; U+0000 – U+0007
                   ("0" %x62) /        ; U+000B (LINE TABULATION)
                   ("0" %x65-66) /     ; U+000E – U+000F
                   ("1" normal-HEXDIG) ; U+0010 – U+001F
                 )
normal-HEXDIG  = DIGIT / %x61-66      ; lowercase hex only
```

#### Index Selector

```abnf
normal-index-selector = "0" / (DIGIT1 *DIGIT)
                        ; non-negative decimal integer (no leading zeros)
```

### 1.3 Canonicalization Rules

| Source Syntax | Normalized Form |
|---|---|
| `$.a` | `$['a']` |
| `$["a"]` | `$['a']` |
| `$[-3]` (array len 5) | `$[2]` |
| `$["\u0061"]` | `$['a']` (U+0061 = 'a', printable → unescaped) |
| `$["\u000B"]` | `$['\u000b']` (U+000B not printable → hex escape, lowercase) |

### 1.4 Examples

| Input Path | Normalized Path | Comment |
|---|---|---|
| `$.a` | `$['a']` | Object value |
| `$[1]` | `$[1]` | Index (already normalized) |
| `$[-3]` | `$[2]` | Negative index (array length = 5) |
| `$.a.b[1:2]` | `$['a']['b'][1]` | Nested structure |
| `$["\u000B"]` | `$['\u000b']` | Unicode escape → lowercase hex |
| `$["\u0061"]` | `$['a']` | Unicode character (printable) |

### 1.5 Usage

- **Testing:** Normalized Paths provide a predictable format for comparing
  expected vs. actual results.
- **Deduplication:** Compare Normalized Paths to detect duplicate nodes.
- **Serialization:** A nodelist can be represented as a JSON array of
  Normalized Path strings.

---

## 2 Semantics of `null`

### 2.1 Core Rule

JSON `null` is treated the **same as any other JSON value**. It does **not**
mean "undefined" or "missing".

### 2.2 Implications

| Scenario | Behavior |
|---|---|
| Selecting `null` value from object/array | Returns `null` as a node value |
| Using `null` as an array (e.g., `$.a[0]` where `a` is `null`) | Selects nothing (not an array) |
| Using `null` as an object (e.g., `$.a.d` where `a` is `null`) | Selects nothing (not an object) |
| Existence test `?@` where `@` is `null` | **true** — the node exists |
| Comparison `@==null` where `@` is `null` | **true** — value equals `null` |
| Comparison `@.d==null` where `@` is `{}` | **false** — `@.d` selects nothing (empty nodelist ≠ `null`) |

### 2.3 Examples

**JSON:**
```json
{"a": null, "b": [null], "c": [{}], "null": 1}
```

| Query | Result | Comment |
|---|---|---|
| `$.a` | `null` | Object value |
| `$.a[0]` | *(empty)* | `null` used as array |
| `$.a.d` | *(empty)* | `null` used as object |
| `$.b[0]` | `null` | Array value |
| `$.b[*]` | `null` | Array value via wildcard |
| `$.b[?@]` | `null` | Existence: node exists |
| `$.b[?@==null]` | `null` | Comparison: value is `null` |
| `$.c[?@.d==null]` | *(empty)* | `@.d` selects nothing → empty ≠ `null` |
| `$.null` | `1` | Member name `"null"`, not JSON null |

### 2.4 Critical Distinction

```
!@.foo        →  true when @.foo selects NO node (missing member)
@.foo == null →  true when @.foo selects a node whose VALUE is null
```

These are **not equivalent**. A missing member and a member with value `null`
are fundamentally different in JSONPath.
