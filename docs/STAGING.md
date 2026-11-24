# Staging endpoint reference

Base URL: `https://api.angry-pisco.stage.assinafy.com.br/v1`.

These ten operations and the assignment `sign_as` field are available on staging. Use the production [API reference](API.md) for shared operations and schemas. Availability on production must be checked before deploying these features there.

## SDK methods

| Operation | SDK method |
|---|---|
| `GET /v1/documents/{documentId}/signer-capacities` | `Assignments.GetSignerCapacitiesAsync` |
| `GET /v1/documents/{documentId}/watchers` | `Documents.ListWatchersAsync` |
| `PUT /v1/documents/{documentId}/watchers` | `Documents.SetWatchersAsync` |
| `POST /v1/accounts/{accountId}/envelopes/{envelopeId}/signers/{signerId}/estimate-resend-cost` | `Envelopes.EstimateResendCostAsync` |
| `GET /v1/documents/{documentId}/notification-history` | `Assignments.GetNotificationHistoryAsync` |
| `POST /v1/documents/{documentId}/scheduled-sends` | `ScheduledSends.ScheduleAssignmentAsync` |
| `GET /v1/accounts/{accountId}/scheduled-sends` | `ScheduledSends.ListAsync` |
| `POST /v1/accounts/{accountId}/scheduled-sends` | `ScheduledSends.ScheduleEnvelopeAsync` |
| `PUT /v1/accounts/{accountId}/scheduled-sends/{scheduledSendId}/cancel` | `ScheduledSends.CancelAsync` |
| `PUT /v1/accounts/{accountId}/scheduled-sends/{scheduledSendId}/send` | `ScheduledSends.SendNowAsync` |

## Requests and responses

Parameters, request bodies, security, success responses and errors are included below. `$ref` values resolve to [API.md](API.md) except `ScheduledSend`, included here. The normal `{ status, message, data }` envelope applies.

### GET /v1/documents/{documentId}/signer-capacities

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Get signer capacities",
  "description": "The capacity each signer of the document's assignment signs in, as the sender gave it with `sign_as` on **Create assignment**: `Sign` for a signer given none. In the order **Get document** lists the assignment's signers; an empty list for a document not sent yet. Reachable with the `documents:read` OAuth scope.",
  "operationId": "6daa15d8655f99845cf17c23d20a9aea",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "responses": {
    "200": {
      "description": "The signers' capacities",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "type": "array",
                    "items": {
                      "required": [
                        "signer_id",
                        "sign_as"
                      ],
                      "properties": {
                        "signer_id": {
                          "type": "string",
                          "example": "615605f50e968054a5b7c9b8"
                        },
                        "sign_as": {
                          "type": "string",
                          "enum": [
                            "Sign",
                            "Approve",
                            "Acknowledge",
                            "Party",
                            "Witness",
                            "Intervening",
                            "Receipt",
                            "IssuerEndorserGuarantor",
                            "IssuerEndorserGuarantorSurety",
                            "Surety",
                            "PartySurety",
                            "JointDebtor",
                            "PartyJointDebtor",
                            "ContractingParty",
                            "ContractedParty",
                            "Landlord",
                            "Tenant",
                            "Buyer",
                            "Seller",
                            "AttorneyInFact",
                            "LegalRepresentative",
                            "Employer",
                            "Employee"
                          ],
                          "example": "Witness"
                        }
                      },
                      "type": "object"
                    }
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/Forbidden"
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Document not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### GET /v1/documents/{documentId}/watchers

```json
{
  "tags": [
    "Documents"
  ],
  "summary": "List a document's watchers",
  "description": "The contacts set with **Set a document's watchers**, in the order they were set. A deleted contact is left out.",
  "operationId": "7ecfff3791dd9808618ba634dff087ba",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "responses": {
    "200": {
      "description": "The document's watchers",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/Signer"
                    }
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/Forbidden",
      "description": "An OAuth application token, a deactivated membership or a role refuses it."
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Document not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### PUT /v1/documents/{documentId}/watchers

```json
{
  "tags": [
    "Documents"
  ],
  "summary": "Set a document's watchers",
  "description": "Replaces the document's watchers: up to 10 contacts of the workspace, each with an e-mail address, who follow the document without signing it. Each is e-mailed once the document ends: declined (naming who declined), expired or signed by all. The e-mail names the document and its sender, and nothing else of it: no link, no file. It is queued, so it goes out shortly after. Naming a contact here is what lets them receive it; the workspace's notification settings don't apply to them. A signer named as a watcher gets this e-mail besides their own. They can be set before the document is sent and changed until it ends; an empty list removes them all.",
  "operationId": "52c71922a6c64fddd4513ce92772491e",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "requestBody": {
    "required": true,
    "content": {
      "application/json": {
        "schema": {
          "required": [
            "watcher_ids"
          ],
          "properties": {
            "watcher_ids": {
              "description": "Contact ids, as **Create signer** answers them; one given twice counts once.",
              "type": "array",
              "items": {
                "type": "string",
                "minLength": 1
              },
              "maxItems": 10,
              "example": [
                "62d6ee35c7741ca4006b9e11"
              ]
            }
          },
          "type": "object"
        }
      }
    }
  },
  "responses": {
    "200": {
      "description": "The document's watchers",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/Signer"
                    }
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "description": "`watcher_ids` missing or not a list of ids; more than 10; an id that is not a contact of the workspace or a contact without an e-mail address; or the document has ended (expired, signed by all, declined, cancelled or failed).",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 400,
            "message": "A document takes at most 10 watchers."
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/Forbidden",
      "description": "An OAuth application token, a deactivated membership or a role refuses it."
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Document not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### POST /v1/accounts/{accountId}/envelopes/{envelopeId}/signers/{signerId}/estimate-resend-cost

```json
{
  "tags": [
    "Envelopes"
  ],
  "summary": "Estimate envelope reminder cost",
  "description": "What reminding a signer of the whole envelope would cost, sending and charging nothing. The reminder is one message per notification method for all of the envelope's documents, so it costs what one document's reminder costs, whatever the number of documents, at the workspace's reminder prices: nothing by e-mail and the WhatsApp resend price by WhatsApp, unless its plan prices them otherwise. Answers as a document's reminder estimate. It does not check whether a reminder would be accepted now: the reminder itself refuses a closed envelope (`409`), and a signer with nothing left to sign or one reminded too recently or too often (`400`).",
  "operationId": "f16c3c68b20af5fdc3758b0484dd3488",
  "parameters": [
    {
      "$ref": "#/components/parameters/AccountId"
    },
    {
      "$ref": "#/components/parameters/EnvelopeId"
    },
    {
      "name": "signerId",
      "in": "path",
      "description": "The signer ID.",
      "required": true,
      "schema": {
        "type": "string"
      }
    }
  ],
  "responses": {
    "200": {
      "description": "Cost estimate",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/ResendCostEstimate"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/DocumentsWriteForbidden"
    },
    "404": {
      "description": "The envelope was not found, or the signer is not one of its signers.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Signer not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### GET /v1/documents/{documentId}/notification-history

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Get notification history with delivery times",
  "description": "Each signer of the document's assignment, in the order **Get document** lists them, with its `notification_history` as **Get document** gives it, each entry adding `delivered_at` and `opened_at`, `null` until then: an e-mail's first delivery and its first open or click; a WhatsApp message's delivery and read as WhatsApp last reported them. `opened_at` is a hint, not proof: a mail app or a link scanner can open a message before anyone reads it, and blocked images or WhatsApp read receipts turned off hide an open. An empty list for a document not sent yet. Reachable with the `documents:read` OAuth scope.",
  "operationId": "4adb1abce1c8086884b47f126ab54f74",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "responses": {
    "200": {
      "description": "The signers' notification history",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "type": "array",
                    "items": {
                      "required": [
                        "signer_id",
                        "notification_history"
                      ],
                      "properties": {
                        "signer_id": {
                          "type": "string",
                          "example": "615605f50e968054a5b7c9b8"
                        },
                        "notification_history": {
                          "type": "array",
                          "items": {
                            "required": [
                              "delivered_at",
                              "opened_at"
                            ],
                            "type": "object",
                            "allOf": [
                              {
                                "$ref": "#/components/schemas/NotificationHistoryEntry"
                              },
                              {
                                "properties": {
                                  "delivered_at": {
                                    "type": [
                                      "string",
                                      "null"
                                    ],
                                    "format": "date-time",
                                    "example": "2026-07-07T12:00:05Z"
                                  },
                                  "opened_at": {
                                    "type": [
                                      "string",
                                      "null"
                                    ],
                                    "format": "date-time",
                                    "example": "2026-07-07T12:30:00Z"
                                  }
                                },
                                "type": "object"
                              }
                            ]
                          }
                        }
                      },
                      "type": "object"
                    }
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/Forbidden"
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Document not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### POST /v1/documents/{documentId}/scheduled-sends

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Schedule an assignment",
  "description": "Keeps a **Create assignment** request and makes it at `send_at`, as the member who scheduled it, through the same checks and charges as **Create assignment** then. Nothing is sent or charged now, and nothing changes for the document: it can still be sent at once, and the scheduled send then fails when it goes. A document takes one pending scheduled send at a time, counting one being sent. The request is checked now as **Estimate assignment cost** checks it (the method, each signer's methods, the plan's features), and each signer named must be a contact of the workspace; the balance is checked when it is sent. A job checks every five minutes and makes the sends that are due. A send that can't be made then becomes `failed` with the reason, nothing is charged, and the member who scheduled it is e-mailed; if that member has left the workspace, it is not made as anyone else: it fails and the workspace's active owners are e-mailed. **List scheduled sends**, **Cancel a scheduled send** and **Send a scheduled send now** follow it.",
  "operationId": "0a37e9f88de36cc3c76d7577ce2322a5",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "requestBody": {
    "required": true,
    "content": {
      "application/json": {
        "schema": {
          "description": "A **Create assignment** body (`method`, `signers`, `entries`, `message`, `expires_at` and the rest, as that route takes them) plus `send_at`.",
          "required": [
            "send_at",
            "signers"
          ],
          "properties": {
            "send_at": {
              "description": "When to send: ISO 8601 with its offset (none means UTC), or a Unix time. From one hour to 90 days ahead.",
              "example": "2026-11-20T09:00:00-03:00",
              "oneOf": [
                {
                  "type": "string",
                  "format": "date-time"
                },
                {
                  "type": "integer"
                }
              ]
            },
            "method": {
              "description": "As in **Create assignment**; `virtual` when left out.",
              "type": "string",
              "enum": [
                "virtual",
                "collect"
              ],
              "example": "virtual"
            },
            "signers": {
              "description": "As in **Create assignment**; each must be a contact of the workspace.",
              "type": "array",
              "items": {
                "type": "object"
              }
            },
            "expires_at": {
              "description": "As in **Create assignment**, an absolute time (ISO 8601 or a Unix time), or null for none; at least two hours after `send_at`, since the request must still have an hour left when it is made.",
              "oneOf": [
                {
                  "type": "string",
                  "format": "date-time"
                },
                {
                  "type": "integer"
                },
                {
                  "type": "null"
                }
              ]
            }
          },
          "type": "object",
          "additionalProperties": true
        }
      }
    }
  },
  "responses": {
    "200": {
      "description": "The scheduled send",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/ScheduledSend"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "description": "`send_at` missing, unreadable, less than an hour or more than 90 days ahead; an `expires_at` less than two hours after it; the document already has an assignment, or a scheduled send pending or being sent; or the request refused as **Estimate assignment cost** refuses it.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 400,
            "message": "The scheduled send must be at least one hour ahead."
          }
        }
      }
    },
    "403": {
      "description": "The workspace's plan lacks a verification or notification method sent (`PlanRestricted`), or an OAuth app's token lacks `documents:write` (`InsufficientScope`); also a deactivated membership or a role.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 403
                  },
                  "message": {
                    "type": "string",
                    "example": "WhatsApp notification is not available for your current plan."
                  },
                  "code": {
                    "type": "string",
                    "enum": [
                      "PlanRestricted",
                      "InsufficientScope"
                    ]
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "404": {
      "description": "The document is not in the workspace, or a signer named is not a contact of it.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Document not found."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### GET /v1/accounts/{accountId}/scheduled-sends

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "List scheduled sends",
  "description": "The workspace's scheduled sends, assignments and envelopes, newest first, whatever their status. Paged: 20 per page by default; the `X-Pagination-Total-Count`, `X-Pagination-Page-Count`, `X-Pagination-Current-Page` and `X-Pagination-Per-Page` headers describe the full list. An OAuth app needs `documents:write` here, not only `documents:read`: a failed send's `failure_reason` can say the workspace's balance ran out or a member left, which an app learns only from a request it can make.",
  "operationId": "75d9f5d4568e8e7cad214320cbf0450a",
  "parameters": [
    {
      "$ref": "#/components/parameters/AccountId"
    },
    {
      "$ref": "#/components/parameters/Page"
    },
    {
      "$ref": "#/components/parameters/PerPage"
    },
    {
      "name": "document_id",
      "in": "query",
      "description": "Only the sends of this document.",
      "required": false,
      "schema": {
        "type": "string",
        "example": "615601fab04c0a3147bb1246"
      }
    }
  ],
  "responses": {
    "200": {
      "description": "The workspace's scheduled sends",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/ScheduledSend"
                    }
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "description": "`document_id` not text.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 400,
            "message": "Give document_id as a document id.",
            "errors": [
              {
                "field": "document_id",
                "code": "Invalid",
                "message": "Give document_id as a document id."
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/DocumentsWriteForbidden"
    },
    "404": {
      "$ref": "#/components/responses/NotFound",
      "description": "The workspace does not exist."
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### POST /v1/accounts/{accountId}/scheduled-sends

```json
{
  "tags": [
    "Envelopes"
  ],
  "summary": "Schedule an envelope",
  "description": "Keeps a **Create envelope** request and makes it at `send_at`, as the member who scheduled it, through the same checks and charges as **Create envelope** then. Nothing is created, sent or charged now, and nothing changes for its documents: each can still be sent at once, and the scheduled send then fails when it goes. A document takes one pending scheduled send at a time, counting one being sent. The request is checked now as **Estimate envelope cost** checks it (`403` with code `PlanRestricted` without the Envelopes feature, `400` with `errors` on the same fields); the balance is checked when it is sent. A job checks every five minutes and makes the sends that are due; failures go as for **Schedule an assignment**: `failed` with the reason, nothing charged, the member who scheduled it e-mailed, or the workspace's active owners if that member has left. An `Idempotency-Key` is not read here: each call keeps one send. An assignment is scheduled on its document (**Schedule an assignment**).",
  "operationId": "629950248a869b5fe0f8d4cecc0c623a",
  "parameters": [
    {
      "$ref": "#/components/parameters/AccountId"
    }
  ],
  "requestBody": {
    "required": true,
    "content": {
      "application/json": {
        "schema": {
          "description": "A **Create envelope** body plus `send_at`; each document's `expires_at` at least two hours after `send_at`.",
          "allOf": [
            {
              "$ref": "#/components/schemas/EnvelopeCreateRequest"
            },
            {
              "required": [
                "send_at"
              ],
              "properties": {
                "send_at": {
                  "description": "When to send: ISO 8601 with its offset (none means UTC), or a Unix time. From one hour to 90 days ahead.",
                  "example": "2026-11-20T09:00:00-03:00",
                  "oneOf": [
                    {
                      "type": "string",
                      "format": "date-time"
                    },
                    {
                      "type": "integer"
                    }
                  ]
                }
              },
              "type": "object"
            }
          ]
        }
      }
    }
  },
  "responses": {
    "200": {
      "description": "The scheduled send",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/ScheduledSend"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "description": "`send_at` missing, unreadable, less than an hour or more than 90 days ahead; a document's `expires_at` less than two hours after it; a document already assigned or in a scheduled send pending or being sent; or the request refused as **Estimate envelope cost** refuses it, in `errors`.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 400,
            "message": "The scheduled send must be at least one hour ahead."
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/EnvelopesFeatureForbidden"
    },
    "404": {
      "$ref": "#/components/responses/NotFound"
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### PUT /v1/accounts/{accountId}/scheduled-sends/{scheduledSendId}/cancel

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Cancel a scheduled send",
  "description": "Cancels a `pending` scheduled send; nothing is sent or charged. A send already being sent, sent, failed or cancelled answers `409`.",
  "operationId": "d1598396bc5fbd8b13895555a8e2c938",
  "parameters": [
    {
      "$ref": "#/components/parameters/AccountId"
    },
    {
      "name": "scheduledSendId",
      "in": "path",
      "description": "The scheduled send ID.",
      "required": true,
      "schema": {
        "type": "string"
      }
    }
  ],
  "responses": {
    "200": {
      "description": "The cancelled scheduled send",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/ScheduledSend"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "403": {
      "$ref": "#/components/responses/DocumentsWriteForbidden"
    },
    "404": {
      "description": "The workspace does not exist, or the scheduled send is not the workspace's.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Scheduled send not found."
          }
        }
      }
    },
    "409": {
      "description": "Only a `pending` scheduled send can be cancelled.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 409,
            "message": "Only a pending scheduled send can be cancelled."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### PUT /v1/accounts/{accountId}/scheduled-sends/{scheduledSendId}/send

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Send a scheduled send now",
  "description": "Makes a `pending` scheduled send's request now rather than at `send_at`: as **Create assignment** or **Create envelope** would make it then, as the member who scheduled it, with the same checks and charges. A refusal answers as that route would answer the member who scheduled it: no documents or credits left, a signer removed, the document already sent, the plan without Envelopes, a `max_total_credits` below the cost now (`409` `CreditCeilingExceeded`), that member gone from the workspace (`400`) or the workspace's deletion pending (`403`); the send then stays `pending`, to go at `send_at` unless cancelled, and no one is e-mailed. A send already being sent, sent, failed or cancelled answers `409`.",
  "operationId": "07b9d3df4de843789391718ee8731e75",
  "parameters": [
    {
      "$ref": "#/components/parameters/AccountId"
    },
    {
      "name": "scheduledSendId",
      "in": "path",
      "description": "The scheduled send ID.",
      "required": true,
      "schema": {
        "type": "string"
      }
    }
  ],
  "responses": {
    "200": {
      "description": "The scheduled send, `sent`, with the assignment or envelope it created",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/ScheduledSend"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "description": "The request refused as **Create assignment** or **Create envelope** refuses it, or the member who scheduled it has left the workspace. The send stays `pending`.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 400,
            "message": "Account does not have enough documents."
          }
        }
      }
    },
    "403": {
      "description": "The workspace's plan lacks a feature the request uses (`PlanRestricted`), or an OAuth app's token lacks `documents:write` (`InsufficientScope`); also a deactivated membership, a role, or the workspace's deletion pending. The send stays `pending`.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 403
                  },
                  "message": {
                    "type": "string",
                    "example": "WhatsApp notification is not available for your current plan."
                  },
                  "code": {
                    "type": "string",
                    "enum": [
                      "PlanRestricted",
                      "InsufficientScope"
                    ]
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "404": {
      "description": "The workspace does not exist, or the scheduled send is not the workspace's; or the document an assignment sends no longer exists (`Document not found.`), and the send stays `pending`.",
      "content": {
        "application/json": {
          "schema": {
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              }
            ]
          },
          "example": {
            "status": 404,
            "message": "Scheduled send not found."
          }
        }
      }
    },
    "409": {
      "description": "Only a `pending` scheduled send can be sent now; or the request's `max_total_credits` is below what it costs now (`CreditCeilingExceeded`, nothing charged), and the send stays `pending`.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "code": {
                    "description": "Only for the ceiling.",
                    "type": "string",
                    "enum": [
                      "CreditCeilingExceeded"
                    ]
                  },
                  "total_credits": {
                    "description": "With `CreditCeilingExceeded`: what the request costs now.",
                    "type": "number",
                    "example": 1.5
                  },
                  "max_total_credits": {
                    "description": "With `CreditCeilingExceeded`: the ceiling it was scheduled with.",
                    "type": "number",
                    "example": 1
                  }
                },
                "type": "object"
              }
            ]
          },
          "example": {
            "status": 409,
            "message": "Only a pending scheduled send can be sent now."
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

## Assignment signer capacity

Set `SignerRef.SignAs` on assignment creation or its estimate. It is fixed at creation, defaults to `Sign`, and is recorded on the certificate page as the sender’s statement. It does not establish authority, and is not accepted by envelope or template signer inputs. Read it using `Assignments.GetSignerCapacitiesAsync`.

### POST /v1/documents/{documentId}/assignments

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Create assignment (request signatures)",
  "description": "Request signatures on a document. Use `method: virtual` to sign without input fields, or `method: collect` to place input fields on specific pages.\n\n**Digital certificate signing works only on `virtual` assignments.** A `DigitalCertificate` signer cannot sign placed fields, so a `collect` assignment with one is refused with `400` before anything is charged.\n\nA document takes one assignment: a retry after a lost response answers `400` and charges nothing. Read the document to see whether the first request went through.\n\nFor **virtual**, the document may be in `uploaded`, `metadata_processing` or `metadata_ready`; it is promoted to `pending_signature` automatically once metadata processing completes. For **collect**, the document must be in `metadata_ready` (fields reference specific pages).\n\n`step` controls signing order: signers sharing a step sign in parallel, and the next step is notified only after the previous step completes. If supplied, every signer must supply it and values must be contiguous starting at 1.",
  "operationId": "d1293d781c9d94ec8b8c43d540c99ef4",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "requestBody": {
    "required": true,
    "content": {
      "application/json": {
        "schema": {
          "required": [
            "method",
            "signers"
          ],
          "properties": {
            "method": {
              "description": "`virtual` signs without input fields; `collect` places input fields on pages. Signers with `DigitalCertificate` verification need `virtual` (`collect` returns `400`).",
              "type": "string",
              "enum": [
                "virtual",
                "collect"
              ],
              "example": "virtual"
            },
            "signers": {
              "type": "array",
              "items": {
                "required": [
                  "id"
                ],
                "properties": {
                  "id": {
                    "type": "string",
                    "example": "615605f50e968054a5b7c9b8"
                  },
                  "verification_method": {
                    "description": "How the signer's identity is verified before signing. `Email` (default) sends a one-time code to the signer's email; `Whatsapp` sends the code over WhatsApp — the verification itself is not billed, but it requires the WhatsApp notification channel, so the signer costs 0.45 credits (available only on paid subscriptions); `DigitalCertificate` has the signer sign with their own ICP-Brasil certificate (A1/A3) — it requires the Digital Certificate feature, the signer must have a CPF or CNPJ in `government_id`, must be alone in its signing step, needs `method: virtual` (a `collect` assignment with a `DigitalCertificate` signer returns `400`), and is charged 0.45 credits per signer. A CPF requires that person's certificate (an e-CPF, or an e-CNPJ naming them as legal representative); a CNPJ requires an e-CNPJ for that company, from any of its representatives. `IdDocumentFaceMatchBasic` verifies the signer from a photo of their government ID, a photo of them holding it and a liveness check — it requires the ID Document + Face Match feature, the signer must have a valid CPF in `government_id`, and is charged a per-signer fee. Omit to default to `Email`.",
                    "type": "string",
                    "enum": [
                      "Email",
                      "Whatsapp",
                      "DigitalCertificate",
                      "IdDocumentFaceMatchBasic"
                    ],
                    "example": "Email"
                  },
                  "notification_methods": {
                    "description": "How the signer is told a signature is being requested. **Exactly one method per signer** — the array shape is historical, and sending two returns `400`. The method must also be compatible with `verification_method`: `Email` verification takes `Email`, `Whatsapp` verification takes `Whatsapp`, and `DigitalCertificate` and `IdDocumentFaceMatchBasic` take either. WhatsApp incurs an additional cost and is available only on paid subscriptions. Omit it to have it inferred from `verification_method`, or `{\"Email\"}` when neither is sent. See **Verification & Notification Methods** for the full pairing table.",
                    "type": "array",
                    "items": {
                      "type": "string",
                      "enum": [
                        "Email",
                        "Whatsapp"
                      ]
                    },
                    "example": [
                      "Email"
                    ]
                  },
                  "embedded": {
                    "description": "Mark this signer as embedded: they sign inside an iframe in your application through a short-lived embed session, and your backend gets no signing link for them (`signing_urls` returns `url: null, embedded: true`). They still receive the normal invitation with their code, and can also sign from it. Requires the Embed plan feature (`403 PlanRestricted` otherwise) and works with Email or Whatsapp verification. Defaults to `false`.",
                    "type": "boolean",
                    "example": false
                  },
                  "step": {
                    "type": "integer",
                    "example": 1
                  },
                  "sign_as": {
                    "description": "The capacity the signer signs in (\"assinar como\"): a party, a witness, a guarantor… `Sign`, plain signing, when left out or `null`. Any other value prints on the signer's row of the certificate page as the sender's statement (for `Witness`, \"Indicado pelo remetente como testemunha\"); neither the invitation nor the signing page shows it. Fixed once the assignment is created and read back with **Get signer capacities**; no other answer or webhook carries it. Matched exactly; anything else, or two values for one signer, answers `400`.",
                    "type": [
                      "string",
                      "null"
                    ],
                    "enum": [
                      "Sign",
                      "Approve",
                      "Acknowledge",
                      "Party",
                      "Witness",
                      "Intervening",
                      "Receipt",
                      "IssuerEndorserGuarantor",
                      "IssuerEndorserGuarantorSurety",
                      "Surety",
                      "PartySurety",
                      "JointDebtor",
                      "PartyJointDebtor",
                      "ContractingParty",
                      "ContractedParty",
                      "Landlord",
                      "Tenant",
                      "Buyer",
                      "Seller",
                      "AttorneyInFact",
                      "LegalRepresentative",
                      "Employer",
                      "Employee",
                      null
                    ],
                    "example": "Witness"
                  }
                },
                "type": "object"
              }
            },
            "entries": {
              "description": "Required for `collect`: field placements per page. In `collect`, every signer listed in `signers` must appear once and own at least one field here; otherwise `400`.",
              "type": "array",
              "items": {
                "properties": {
                  "page_id": {
                    "type": "string"
                  },
                  "fields": {
                    "type": "array",
                    "items": {
                      "properties": {
                        "signer_id": {
                          "type": "string"
                        },
                        "field_id": {
                          "type": "string"
                        },
                        "display_settings": {
                          "$ref": "#/components/schemas/DisplaySettings"
                        }
                      },
                      "type": "object"
                    }
                  }
                },
                "type": "object"
              }
            },
            "message": {
              "description": "Text included in the invitation email.",
              "type": "string"
            },
            "expires_at": {
              "description": "ISO 8601; default is no expiration. Must be at least one hour in the future. A UTC offset in the value is honored (`2026-12-31T23:59:59-03:00` is `2027-01-01T02:59:59Z`); a value without one is read as UTC, and a date alone (`2026-12-31`) means 23:59 UTC that day.",
              "type": "string",
              "format": "date-time"
            },
            "copy_receivers": {
              "description": "Signer IDs that only receive a copy.",
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "max_total_credits": {
              "description": "Optional ceiling on what this request may spend: refused with `409` and code `CreditCeilingExceeded`, nothing charged, when its `total_credits` is above this. Send the `total_credits` of the last estimate. A value that is not a number of credits, zero or more, answers `400`; numeric text counts, as a form-encoded body sends it.",
              "type": "number",
              "example": 1.5
            }
          },
          "type": "object"
        },
        "examples": {
          "virtual_full": {
            "$ref": "#/components/examples/AssignmentCreateVirtualFull"
          },
          "collect_full": {
            "$ref": "#/components/examples/AssignmentCreateCollectFull"
          },
          "virtual": {
            "$ref": "#/components/examples/AssignmentCreateVirtual"
          },
          "collect": {
            "$ref": "#/components/examples/AssignmentCreateCollect"
          }
        }
      }
    }
  },
  "responses": {
    "200": {
      "description": "The created assignment",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/Assignment"
                  }
                },
                "type": "object"
              }
            ]
          },
          "examples": {
            "virtual": {
              "$ref": "#/components/examples/AssignmentCreatedVirtual"
            },
            "collect": {
              "$ref": "#/components/examples/AssignmentCreatedCollect"
            }
          }
        }
      }
    },
    "400": {
      "$ref": "#/components/responses/BadRequest",
      "description": "The body cannot make an assignment: a signer, method, step, field or expiration is missing or invalid, the document is not ready or already has one, or the workspace lacks the documents or credits it needs (e.g. `At least one signer is required.`)."
    },
    "409": {
      "$ref": "#/components/responses/CreditCeilingExceeded"
    },
    "403": {
      "description": "The workspace's plan lacks a verification or notification method sent (`PlanRestricted`), or an OAuth app's token lacks `documents:write` (`InsufficientScope`); also a deactivated membership or a role.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 403
                  },
                  "message": {
                    "type": "string",
                    "example": "WhatsApp notification is not available for your current plan."
                  },
                  "code": {
                    "type": "string",
                    "enum": [
                      "PlanRestricted",
                      "InsufficientScope"
                    ],
                    "example": "PlanRestricted"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 404
                  },
                  "message": {
                    "type": "string",
                    "example": "Document not found."
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

### POST /v1/documents/{documentId}/assignments/estimate-cost

```json
{
  "tags": [
    "Assignments"
  ],
  "summary": "Estimate assignment cost",
  "description": "Estimate the cost of creating an assignment without creating it, returning a cost breakdown and the current account balances. Signer IDs are not required — only the verification/notification method affects cost. Each assignment consumes 1 document from the plan allowance; if exhausted, an extra document is charged from credits (`needs_extra_document` = true). `blocking_reason` may be `PendingPayment`, `InsufficientDocuments` or `InsufficientCredits`.\n\n### Pricing\n\nPer-unit costs (in credits) used to build the estimate:\n\n| Item | Cost |\n|------|------|\n| Extra document | 1 credit |\n| Email notification | 0 credits |\n| WhatsApp notification | depends on the plan, see the table below |\n| Digital certificate signature (per signer) | 0.45 credits |\n| Identity document verification (per signer) | depends on the plan, see the table below |\n\nWhatsApp notification, per plan:\n\n| Plan | Price per signer |\n|------|------------------|\n| Pro | 0.4 credits |\n| Other plans and enterprise contracts | 0.45 credits |\n\nIdentity document verification, per plan:\n\n| Plan | Price per signer |\n|------|------------------|\n| Free | 1.5 credits |\n| Standard | 1 credit |\n| Pro | 0.5 credits |\n| Other plans and enterprise contracts | 0.25 credits |\n\n`Email` and `Whatsapp` verification are not priced separately — their only line in the `breakdown` is their notification. A `Whatsapp`-verified signer therefore shows up as a WhatsApp notification, because that channel is mandatory for that verification method. A `DigitalCertificate` signer adds the digital-certificate signature cost **on top of** its notification cost; it appears in the `breakdown` under the `SignatureDigitalCertificate` code. An `IdDocumentFaceMatchBasic` signer likewise adds your plan's verification price on top of its notification, under the `VerificationIdDocumentFaceMatchBasic` code.",
  "operationId": "f3910833e526a79d16f6437ad6a8d215",
  "parameters": [
    {
      "$ref": "#/components/parameters/DocumentId"
    }
  ],
  "requestBody": {
    "required": true,
    "content": {
      "application/json": {
        "schema": {
          "properties": {
            "method": {
              "description": "Send the method you will create the assignment with. With `collect`, a `DigitalCertificate` signer returns `400`, because certificate signing works only on `virtual` assignments.",
              "type": "string",
              "enum": [
                "virtual",
                "collect"
              ],
              "example": "virtual"
            },
            "signers": {
              "description": "Required for `virtual`; each entry may be `{}` to default to Email.",
              "type": "array",
              "items": {
                "properties": {
                  "verification_method": {
                    "description": "Verification method to price. `Whatsapp` forces the WhatsApp notification, so it prices at 0.45 credits per signer; `DigitalCertificate` adds the per-signer signature cost on top of its notification, and with `method: collect` returns `400` (certificate signers sign only virtual assignments). `IdDocumentFaceMatchBasic` requires its account feature, requires the signer to have a valid CPF in `government_id`, and is charged your plan's per-signer verification price on top of its notification.",
                    "type": "string",
                    "enum": [
                      "Email",
                      "Whatsapp",
                      "DigitalCertificate",
                      "IdDocumentFaceMatchBasic"
                    ],
                    "example": "Whatsapp"
                  },
                  "sign_as": {
                    "description": "Checked as **Create assignment** checks it (`400` for anything else); `null` reads as `Sign`; it doesn't change the price.",
                    "type": [
                      "string",
                      "null"
                    ],
                    "enum": [
                      "Sign",
                      "Approve",
                      "Acknowledge",
                      "Party",
                      "Witness",
                      "Intervening",
                      "Receipt",
                      "IssuerEndorserGuarantor",
                      "IssuerEndorserGuarantorSurety",
                      "Surety",
                      "PartySurety",
                      "JointDebtor",
                      "PartyJointDebtor",
                      "ContractingParty",
                      "ContractedParty",
                      "Landlord",
                      "Tenant",
                      "Buyer",
                      "Seller",
                      "AttorneyInFact",
                      "LegalRepresentative",
                      "Employer",
                      "Employee",
                      null
                    ]
                  },
                  "notification_methods": {
                    "description": "The notification channel to price — exactly one per signer, subject to the same pairing rules as **Create assignment**. `Whatsapp` costs 0.45 credits per signer. Omit it to have it inferred from `verification_method`.",
                    "type": "array",
                    "items": {
                      "type": "string",
                      "enum": [
                        "Email",
                        "Whatsapp"
                      ]
                    }
                  }
                },
                "type": "object"
              }
            },
            "entries": {
              "description": "Required for `collect`.",
              "type": "array",
              "items": {
                "type": "object"
              }
            }
          },
          "type": "object"
        }
      }
    }
  },
  "responses": {
    "200": {
      "description": "Cost estimate and balances",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/Envelope"
              },
              {
                "properties": {
                  "data": {
                    "$ref": "#/components/schemas/CostEstimate"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "400": {
      "$ref": "#/components/responses/BadRequest",
      "description": "The body cannot make an assignment: the same checks of signers, methods and steps as creating it, and the document must be able to take one (e.g. `At least one signer is required.`)."
    },
    "403": {
      "description": "The workspace's plan lacks a verification or notification method sent (`PlanRestricted`), or an OAuth app's token lacks `documents:write` (`InsufficientScope`); also a deactivated membership or a role.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 403
                  },
                  "message": {
                    "type": "string",
                    "example": "WhatsApp notification is not available for your current plan."
                  },
                  "code": {
                    "type": "string",
                    "enum": [
                      "PlanRestricted",
                      "InsufficientScope"
                    ],
                    "example": "PlanRestricted"
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "404": {
      "description": "The document is not in the workspace.",
      "content": {
        "application/json": {
          "schema": {
            "type": "object",
            "allOf": [
              {
                "$ref": "#/components/schemas/ErrorEnvelope"
              },
              {
                "properties": {
                  "status": {
                    "type": "integer",
                    "example": 404
                  },
                  "message": {
                    "type": "string",
                    "example": "Document not found."
                  }
                },
                "type": "object"
              }
            ]
          }
        }
      }
    },
    "401": {
      "$ref": "#/components/responses/Unauthorized"
    },
    "500": {
      "$ref": "#/components/responses/ServerError"
    }
  },
  "security": [
    {
      "bearerAuth": []
    },
    {
      "apiKeyAuth": []
    }
  ]
}
```

## ScheduledSend schema

```json
{
  "description": "A **Create assignment** or **Create envelope** request kept until `send_at`, then made as that route makes it, by the member who scheduled it.",
  "required": [
    "id",
    "type",
    "document_ids",
    "status",
    "send_at",
    "assignment_id",
    "envelope_id",
    "failure_reason",
    "created_at",
    "processed_at"
  ],
  "properties": {
    "resource": {
      "type": "string",
      "example": "scheduled_send"
    },
    "id": {
      "type": "string",
      "example": "64f1a2b3c4d5e6f708192a3b"
    },
    "type": {
      "description": "What it creates.",
      "type": "string",
      "enum": [
        "assignment",
        "envelope"
      ],
      "example": "assignment"
    },
    "document_ids": {
      "description": "The documents it sends: one for an assignment, the envelope's for an envelope. A deleted document is left out.",
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "615601fab04c0a3147bb1246"
      ]
    },
    "status": {
      "description": "`pending` until `send_at` (or **Send a scheduled send now**); `processing` while its request is being made; then `sent` (the request was made; its invitations go out right after), `failed` or, if cancelled while pending, `cancelled`. A send whose run was interrupted while `processing` made nothing, and fails within about an hour.",
      "type": "string",
      "enum": [
        "pending",
        "processing",
        "sent",
        "cancelled",
        "failed"
      ],
      "example": "pending"
    },
    "send_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-11-20T12:00:00Z"
    },
    "assignment_id": {
      "description": "The assignment it created, once an `assignment` is `sent`.",
      "type": [
        "string",
        "null"
      ],
      "example": null
    },
    "envelope_id": {
      "description": "The envelope it created, once an `envelope` is `sent`; `null` again once the envelope is deleted.",
      "type": [
        "string",
        "null"
      ],
      "example": null
    },
    "failure_reason": {
      "description": "Why it was not sent, once `failed`: the refusal the create route answered then (no documents or credits left, a signer removed, the document already sent, the plan without Envelopes), that the workspace is being deleted, or that the member who scheduled it left the workspace. Written in Portuguese.",
      "type": [
        "string",
        "null"
      ],
      "example": null
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-11-14T18:00:00Z"
    },
    "processed_at": {
      "description": "When it was sent, failed or cancelled.",
      "type": [
        "string",
        "null"
      ],
      "format": "date-time",
      "example": null
    }
  },
  "type": "object"
}
```
