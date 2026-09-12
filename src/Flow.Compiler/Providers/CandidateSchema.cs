namespace Flow.Compiler.Providers;

public static class CandidateSchema
{
    public static readonly string Json = """
    {
      "type": "object",
      "properties": {
        "workflow": { "$ref": "#/$defs/workflowBody" },
        "interpretations": { "type": "array", "items": { "type": "string" } },
        "assumptions": { "type": "array", "items": { "type": "string" } },
        "unresolved": { "type": "array", "items": { "type": "string" } }
      },
      "required": ["workflow", "interpretations", "assumptions", "unresolved"],
      "additionalProperties": false,
      "$defs": {
        "namedExpression": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "value": { "$ref": "#/$defs/expression" }
          },
          "required": ["name", "value"],
          "additionalProperties": false
        },
        "workflowBody": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "nodes": { "type": "array", "items": { "$ref": "#/$defs/node" } },
            "return": { "type": "array", "items": { "$ref": "#/$defs/namedExpression" } }
          },
          "required": ["name", "nodes", "return"],
          "additionalProperties": false
        },
        "node": {
          "anyOf": [
            { "$ref": "#/$defs/callNode" },
            { "$ref": "#/$defs/ifNode" },
            { "$ref": "#/$defs/filterNode" },
            { "$ref": "#/$defs/sortNode" },
            { "$ref": "#/$defs/aggregateNode" },
            { "$ref": "#/$defs/foreachNode" },
            { "$ref": "#/$defs/assertNode" }
          ]
        },
        "callNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "call" },
            "id": { "type": "string" },
            "tool": { "type": "string" },
            "arguments": { "type": "array", "items": { "$ref": "#/$defs/namedExpression" } }
          },
          "required": ["kind", "id", "tool", "arguments"],
          "additionalProperties": false
        },
        "ifBranch": {
          "type": "object",
          "properties": {
            "nodes": { "type": "array", "items": { "$ref": "#/$defs/node" } },
            "value": { "$ref": "#/$defs/expression" }
          },
          "required": ["nodes", "value"],
          "additionalProperties": false
        },
        "ifNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "if" },
            "id": { "type": "string" },
            "condition": { "$ref": "#/$defs/expression" },
            "trueBranch": { "$ref": "#/$defs/ifBranch" },
            "falseBranch": { "$ref": "#/$defs/ifBranch" }
          },
          "required": ["kind", "id", "condition", "trueBranch", "falseBranch"],
          "additionalProperties": false
        },
        "filterNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "filter" },
            "id": { "type": "string" },
            "source": { "$ref": "#/$defs/expression" },
            "parameterName": { "type": "string" },
            "predicate": { "$ref": "#/$defs/expression" }
          },
          "required": ["kind", "id", "source", "parameterName", "predicate"],
          "additionalProperties": false
        },
        "sortNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "sort" },
            "id": { "type": "string" },
            "source": { "$ref": "#/$defs/expression" },
            "parameterName": { "type": "string" },
            "key": { "$ref": "#/$defs/expression" },
            "direction": { "type": "string", "enum": ["Ascending", "Descending"] }
          },
          "required": ["kind", "id", "source", "parameterName", "key", "direction"],
          "additionalProperties": false
        },
        "aggregateNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "aggregate" },
            "id": { "type": "string" },
            "source": { "$ref": "#/$defs/expression" },
            "operation": { "type": "string", "enum": ["Sum", "Count", "First"] },
            "parameterName": { "type": ["string", "null"] },
            "selector": { "anyOf": [{ "$ref": "#/$defs/expression" }, { "type": "null" }] }
          },
          "required": ["kind", "id", "source", "operation", "parameterName", "selector"],
          "additionalProperties": false
        },
        "foreachNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "foreach" },
            "id": { "type": "string" },
            "source": { "$ref": "#/$defs/expression" },
            "parameterName": { "type": "string" },
            "limit": { "type": "integer" },
            "body": { "type": "array", "items": { "$ref": "#/$defs/node" } },
            "bodyValue": { "$ref": "#/$defs/expression" }
          },
          "required": ["kind", "id", "source", "parameterName", "limit", "body", "bodyValue"],
          "additionalProperties": false
        },
        "assertNode": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "assert" },
            "id": { "type": "string" },
            "condition": { "$ref": "#/$defs/expression" },
            "failureCode": { "type": "string" }
          },
          "required": ["kind", "id", "condition", "failureCode"],
          "additionalProperties": false
        },
        "expression": {
          "anyOf": [
            { "$ref": "#/$defs/pathExpression" },
            { "$ref": "#/$defs/constantExpression" },
            { "$ref": "#/$defs/binaryExpression" },
            { "$ref": "#/$defs/objectExpression" }
          ]
        },
        "pathExpression": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "path" },
            "path": { "type": "string" }
          },
          "required": ["kind", "path"],
          "additionalProperties": false
        },
        "constantExpression": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "constant" },
            "value": { "type": ["string", "number", "boolean", "null"] },
            "source": { "type": ["string", "null"] },
            "detail": { "type": ["string", "null"] }
          },
          "required": ["kind", "value", "source", "detail"],
          "additionalProperties": false
        },
        "binaryExpression": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "binary" },
            "left": { "$ref": "#/$defs/expression" },
            "operator": {
              "type": "string",
              "enum": ["Equal", "NotEqual", "LessThan", "LessThanOrEqual", "GreaterThan", "GreaterThanOrEqual", "And", "Or"]
            },
            "right": { "$ref": "#/$defs/expression" }
          },
          "required": ["kind", "left", "operator", "right"],
          "additionalProperties": false
        },
        "objectExpression": {
          "type": "object",
          "properties": {
            "kind": { "type": "string", "const": "object" },
            "fields": { "type": "array", "items": { "$ref": "#/$defs/namedExpression" } }
          },
          "required": ["kind", "fields"],
          "additionalProperties": false
        }
      }
    }
    """;
}
