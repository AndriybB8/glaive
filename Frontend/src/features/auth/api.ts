import {request} from '../../lib/http'

import type {RegisterPayload, User} from "./types.ts";

const BASE_URL = '/api/auth'

export function register(payload: RegisterPayload): Promise<User> {
  return request(`${BASE_URL}/register`, {method: 'POST', body: JSON.stringify(payload)})
}
