// Håndskrevne enum-verdier. Backend serialiserer C#-enumer som heltall, og
// OpenAPI-dokumentet har ingen verdiliste, så navnene må holdes i synk manuelt.

/** Speiler `AuthStatus` i Services/Authentication/AuthResponse.cs. */
export const AuthStatus = {
  Success: 0,
  AuthenticationFailed: 1,
  InvalidUsername: 2,
} as const;
export type AuthStatus = (typeof AuthStatus)[keyof typeof AuthStatus];

/** Speiler `OtpStatus` i Services/Authentication/OtpResponse.cs. */
export const OtpStatus = {
  Sent: 0,
  InvalidPhoneNumber: 1,
  TooManyRequests: 2,
} as const;
export type OtpStatus = (typeof OtpStatus)[keyof typeof OtpStatus];
