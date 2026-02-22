---
title: "RFC 9535 JSONPath – Overview, Terminology & Data Model"
description: >
  Authoritative reference for implementing RFC 9535 (JSONPath: Query Expressions
  for JSON). Covers core concepts, terminology, the node/tree data model, and
  high-level query structure.
source: "https://www.rfc-editor.org/rfc/rfc9535"
globs:
  - "**/*.cs"
  - "**/*.json"
---

# RFC 9535 – JSONPath Overview

> **Canonical source:** RFC 9535 — *JSONPath: Query Expressions for JSON*
> (February 2024, Standards Track).

## 1 Purpose

JSONPath defines a **string syntax** for selecting and extracting JSON values
from within a given JSON value (the *query argument*). The output of a query is
a **nodelist** — a list of zero or more nodes.

## 2 Core Terminology

| Term | Definition |
|---|---|
| **Value** | A JSON data item: primitive (number, string, `null`, `true`, `false`) or structured (object, array). |
| **Member** | A name/value pair in a JSON object. |
| **Name** | The string portion of a member. |
| **Element** | A value in a JSON array. |
| **Index** | An integer that identifies a specific array element. |
| **Query** | A JSONPath expression. |
| **Query Argument** | The JSON value a query is applied to. |
| **Location** | The position of a value within the query argument, representable as a Normalized Path. |
| **Node** | The pair of a value and its location within the query argument. |
| **Root Node** | The unique node whose value is the entire query argument. |
| **Root Node Identifier** | `$` — refers to the root node. |
| **Current Node Identifier** | `@` — refers to the current node inside a filter expression. |
| **Children** | For arrays: element nodes. For objects: member-value nodes. Primitives have no children. |
| **Descendants** | Transitive closure of the children relation. |
| **Depth** | Number of ancestors. Root = 0, its children = 1, etc. |
| **Nodelist** | An ordered list of nodes — the result type of every query. |
| **Segment** | A construct that selects children (`[<selectors>]`) or descendants (`..[<selectors>]`). |
| **Selector** | A single item inside a segment that produces a nodelist of child nodes. |
| **Singular Query** | A syntactically restricted query that always produces at most one node. |
| **Normalized Path** | A canonical JSONPath form that uniquely identifies one node (see dedicated file). |
| **Unicode Scalar Value** | Any Unicode code point except surrogates (0–D7FF, E000–10FFFF). Queries are sequences of these. |

## 3 Data Model — JSON Values as Trees of Nodes

- The query argument is modelled as a **tree** of nodes.
- A node is either the **root node** or one of its **descendants**.
- Only **member values** of objects are nodes (member names are not selectable).
- The result of applying a query is a **nodelist**.

## 4 High-Level Query Structure

```
jsonpath-query = root-identifier segments
segments       = *(S segment)
```

A JSONPath query is:

1. The **root identifier** `$` (produces a one-node nodelist containing the root).
2. Followed by zero or more **segments**, each applied to the previous result.

### 4.1 Identifiers

| Identifier | Meaning |
|---|---|
| `$` | Root node of the query argument. |
| `@` | Current node (valid only inside filter expressions). |

### 4.2 Segments (summary)

| Notation | Kind |
|---|---|
| `[<selectors>]` | Child segment — selects children. |
| `.name` / `.*` | Shorthand child segment. |
| `..[<selectors>]` | Descendant segment — selects descendants. |
| `..name` / `..*` | Shorthand descendant segment. |

### 4.3 Selectors (summary)

| Syntax | Selector |
|---|---|
| `'name'` or `"name"` | **Name selector** — selects a named child of an object. |
| `*` | **Wildcard** — all children of an object or array. |
| `3` (integer) | **Index selector** — selects an indexed child of an array. |
| `start:end:step` | **Array slice** — selects a series of array elements. |
| `?<logical-expr>` | **Filter selector** — selects children matching a logical expression. |
| `fname(...)` | **Function extension** — invoked inside a filter expression. |

## 5 Evaluation Semantics

1. `$` produces a one-element nodelist (the root node).
2. Each segment operates on **every node** in its input nodelist.
3. Per-node result nodelists are **concatenated** in input order.
4. A node may appear more than once — **duplicates are NOT removed**.
5. If any segment produces an empty nodelist, the whole query may produce empty.
6. A valid segment **never raises an error** at evaluation time; structural
   mismatches yield fewer (or zero) selected nodes.

## 6 Well-Formedness & Validity

A query string must be both **well-formed** (conforms to ABNF) and **valid**:

1. Integer literals must be in the I-JSON exact range: `[-(2^53)+1, (2^53)-1]`.
2. Function extension uses must be **well-typed** (see function extensions file).

Implementations **MUST** raise an error for any query that is not well-formed
and valid. This check is **independent** of the query argument.

## 7 Encoding

- Queries **MUST** be encoded in UTF-8.
- The grammar assumes UTF-8 has been decoded to Unicode scalar values first.

## 8 Bookstore Example (reference data)

```json
{ "store": {
    "book": [
      { "category": "reference",
        "author": "Nigel Rees",
        "title": "Sayings of the Century",
        "price": 8.95
      },
      { "category": "fiction",
        "author": "Evelyn Waugh",
        "title": "Sword of Honour",
        "price": 12.99
      },
      { "category": "fiction",
        "author": "Herman Melville",
        "title": "Moby Dick",
        "isbn": "0-553-21311-3",
        "price": 8.99
      },
      { "category": "fiction",
        "author": "J. R. R. Tolkien",
        "title": "The Lord of the Rings",
        "isbn": "0-395-19395-8",
        "price": 22.99
      }
    ],
    "bicycle": {
      "color": "red",
      "price": 399
    }
  }
}
```

| Query | Intended Result |
|---|---|
| `$.store.book[*].author` | The authors of all books in the store |
| `$..author` | All authors |
| `$.store.*` | All things in the store (books + bicycle) |
| `$.store..price` | The prices of everything in the store |
| `$..book[2]` | The third book |
| `$..book[-1]` | The last book in order |
| `$..book[0,1]` / `$..book[:2]` | The first two books |
| `$..book[?@.isbn]` | All books with an ISBN number |
| `$..book[?@.price<10]` | All books cheaper than 10 |
| `$..*` | All member values and array elements |

## 9 Related Instruction Files

- **ABNF Grammar** → `rfc9535-abnf-grammar.instructions.md`
- **Selectors** → `rfc9535-selectors.instructions.md`
- **Filter Expressions** → `rfc9535-filter-expressions.instructions.md`
- **Function Extensions** → `rfc9535-function-extensions.instructions.md`
- **Segments** → `rfc9535-segments.instructions.md`
- **Normalized Paths & Null** → `rfc9535-normalized-paths-null.instructions.md`
- **Examples & Test Cases** → `rfc9535-examples-tests.instructions.md`
