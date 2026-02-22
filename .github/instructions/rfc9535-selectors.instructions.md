---
title: "RFC 9535 JSONPath – Selectors (Name, Wildcard, Index, Slice)"
description: >
  Detailed reference for the four non-filter selectors defined in RFC 9535:
  name selector, wildcard selector, index selector, and array slice selector.
  Includes syntax, semantics, and normative examples.
source: "https://www.rfc-editor.org/rfc/rfc9535#section-2.3"
globs:
  - "**/*.cs"
---

# RFC 9535 – Selectors

Selectors appear **only** inside child segments and descendant segments. Each
selector produces a nodelist of zero or more children of its input node.

```abnf
selector = name-selector /
           wildcard-selector /
           slice-selector /
           index-selector /
           filter-selector      ; covered in filter-expressions file
```

---

## 1 Name Selector

### 1.1 Syntax

```abnf
name-selector  = string-literal

string-literal = %x22 *double-quoted %x22 /   ; "string"
                 %x27 *single-quoted %x27      ; 'string'
```

- Strings may be enclosed in **single** or **double** quotes.
- Escape sequences follow JSON conventions — see the ABNF grammar file for the
  full `escapable` / `hexchar` rules.

### 1.2 Semantics

1. Remove surrounding quotes and resolve escape sequences to produce a member
   name **M**.
2. When applied to an **object** node, select the member value whose name equals
   **M**. If no such member exists, select nothing (no error).
3. When applied to a non-object node, select nothing.

**String comparison**: Two strings are equal **if and only if** they are
identical sequences of Unicode scalar values. No normalization (NFC, NFD, etc.)
is applied.

### 1.3 Escape Sequence Table

| Escape | Unicode | Description |
|---|---|---|
| `\b` | U+0008 | BS backspace |
| `\t` | U+0009 | HT horizontal tab |
| `\n` | U+000A | LF line feed |
| `\f` | U+000C | FF form feed |
| `\r` | U+000D | CR carriage return |
| `\"` | U+0022 | quotation mark |
| `\'` | U+0027 | apostrophe |
| `\/` | U+002F | slash (solidus) |
| `\\` | U+005C | backslash |
| `\uXXXX` | varies | hex escape (surrogate pairs for > U+FFFF) |

### 1.4 Examples

**JSON:**
```json
{
  "o": {"j j": {"k.k": 3}},
  "'": {"@": 2}
}
```

| Query | Result | Normalized Paths |
|---|---|---|
| `$.o['j j']` | `{"k.k": 3}` | `$['o']['j j']` |
| `$.o['j j']['k.k']` | `3` | `$['o']['j j']['k.k']` |
| `$.o["j j"]["k.k"]` | `3` | `$['o']['j j']['k.k']` |
| `$["'"]["@"]` | `2` | `$['\'']['@']` |

---

## 2 Wildcard Selector

### 2.1 Syntax

```abnf
wildcard-selector = "*"
```

### 2.2 Semantics

- Selects **all children** of an object or array node.
- **Object** children: order is **not stipulated** (JSON objects are unordered).
  Different evaluation runs may yield different orderings.
- **Array** children: order matches array order.
- Applied to a **primitive** value (number, string, `true`, `false`, `null`):
  selects nothing.

> Children of an object are its **member values**, not member names.

### 2.3 Examples

**JSON:**
```json
{
  "o": {"j": 1, "k": 2},
  "a": [5, 3]
}
```

| Query | Result | Comment |
|---|---|---|
| `$[*]` | `{"j": 1, "k": 2}`, `[5, 3]` | Object values |
| `$.o[*]` | `1`, `2` | Object values |
| `$.o[*]` | `2`, `1` | Alternative result (order not stipulated) |
| `$.o[*, *]` | `1`, `2`, `2`, `1` | Non-deterministic ordering; duplicates kept |
| `$.a[*]` | `5`, `3` | Array elements in order |

---

## 3 Index Selector

### 3.1 Syntax

```abnf
index-selector = int

int            = "0" / (["-"] DIGIT1 *DIGIT)
DIGIT1         = %x31-39     ; 1-9
```

- A decimal integer, optionally negative.
- No leading zeros (`01` is invalid).
- Must be in I-JSON exact range: `[-(2^53)+1, (2^53)-1]`.

### 3.2 Semantics

- **Non-negative index**: zero-based. `0` → first element, `4` → fifth.
- **Negative index**: counts from the end. The effective index is
  `array_length + index`. `-1` → last element, `-2` → penultimate.
- If the effective index is outside the array bounds → selects nothing (no error).
- Applied to a non-array node → selects nothing.

### 3.3 Examples

**JSON:**
```json
["a", "b"]
```

| Query | Result | Normalized Path | Comment |
|---|---|---|---|
| `$[1]` | `"b"` | `$[1]` | Element of array |
| `$[-2]` | `"a"` | `$[0]` | Element from end |

---

## 4 Array Slice Selector

### 4.1 Syntax

```abnf
slice-selector = [start S] ":" S [end S] [":" [S step]]

start = int     ; included in selection
end   = int     ; NOT included in selection
step  = int     ; default: 1
```

All three parameters are optional. The second colon can be omitted when `step`
is omitted.

### 4.2 Semantics

#### 4.2.1 Defaults

| Condition | `start` default | `end` default |
|---|---|---|
| `step >= 0` | `0` | `len` |
| `step < 0` | `len - 1` | `-len - 1` |

Default `step` is **1**.

#### 4.2.2 Normalize Function

```
FUNCTION Normalize(i, len):
  IF i >= 0 THEN RETURN i
  ELSE           RETURN len + i
```

#### 4.2.3 Bounds Function

```
FUNCTION Bounds(start, end, step, len):
  n_start = Normalize(start, len)
  n_end   = Normalize(end, len)

  IF step >= 0 THEN
    lower = MIN(MAX(n_start, 0), len)
    upper = MIN(MAX(n_end, 0), len)
  ELSE
    upper = MIN(MAX(n_start, -1), len - 1)
    lower = MIN(MAX(n_end, -1), len - 1)
  END IF

  RETURN (lower, upper)
```

#### 4.2.4 Selection

```
IF step > 0 THEN
  i = lower
  WHILE i < upper:
    SELECT a(i)
    i = i + step

ELSE IF step < 0 THEN
  i = upper
  WHILE lower < i:
    SELECT a(i)
    i = i + step
```

- When `step = 0`, **no elements** are selected (empty result).
- Applied to a non-array node → selects nothing.

### 4.3 Examples

**JSON:**
```json
["a", "b", "c", "d", "e", "f", "g"]
```

| Query | Result | Comment |
|---|---|---|
| `$[1:3]` | `"b"`, `"c"` | Default step = 1 |
| `$[5:]` | `"f"`, `"g"` | No end → through end of array |
| `$[1:5:2]` | `"b"`, `"d"` | Step 2 |
| `$[5:1:-2]` | `"f"`, `"d"` | Negative step |
| `$[::-1]` | `"g"` … `"a"` | Full reverse |

---

## 5 Implementation Notes

- All selectors select **nothing** (rather than error) when the input node type
  does not match (e.g., index applied to an object).
- The wildcard selector may produce results in **different orders** across runs
  when applied to objects.
- Duplicate nodes are **preserved** in the nodelist.
