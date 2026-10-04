// Autogenerert av npm run gen:api — ikke rediger.
// Kilde: src/Middagsasen.Planner.Api/openapi/openapi.json

import type { components } from "./api";

type Schemas = components["schemas"];

/** Enum-schemaet `ApprovalFilter` i OpenAPI-dokumentet. */
export const ApprovalFilter = {
  All: 0,
  Approved: 1,
  Rejected: 2,
  Pending: 3,
} as const satisfies Record<string, Schemas["ApprovalFilter"]>;
export type ApprovalFilter = (typeof ApprovalFilter)[keyof typeof ApprovalFilter];

/** Enum-schemaet `ApprovalStatus` i OpenAPI-dokumentet. */
export const ApprovalStatus = {
  Approved: 1,
  Rejected: 2,
} as const satisfies Record<string, Schemas["ApprovalStatus"]>;
export type ApprovalStatus = (typeof ApprovalStatus)[keyof typeof ApprovalStatus];

/** Enum-schemaet `AuthStatus` i OpenAPI-dokumentet. */
export const AuthStatus = {
  Success: 0,
  AuthenticationFailed: 1,
  InvalidUsername: 2,
} as const satisfies Record<string, Schemas["AuthStatus"]>;
export type AuthStatus = (typeof AuthStatus)[keyof typeof AuthStatus];

/** Enum-schemaet `OtpStatus` i OpenAPI-dokumentet. */
export const OtpStatus = {
  Sent: 0,
  InvalidPhoneNumber: 1,
  TooManyRequests: 2,
} as const satisfies Record<string, Schemas["OtpStatus"]>;
export type OtpStatus = (typeof OtpStatus)[keyof typeof OtpStatus];
