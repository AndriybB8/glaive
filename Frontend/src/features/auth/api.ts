import {request} from '../../lib/http'

import type {AuthToken, LoginPayload, RegisterPayload, User} from "./types.ts";

const BASE_URL = '/api/auth'

export function register(payload: RegisterPayload): Promise<User> {
  return request(`${BASE_URL}/register`, {method: 'POST', body: JSON.stringify(payload)})
}

export function login(payload: LoginPayload): Promise<AuthToken> {
  return request(`${BASE_URL}/login`, {method: 'POST', body: JSON.stringify(payload)})
}
