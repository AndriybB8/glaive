export interface User {
  id: string
  email: string
  createdAt: string
  status: 'Active' | 'Disabled'
}

export interface RegisterPayload {
  email: string
  password: string
}

export interface LoginPayload {
  email: string
  password: string
}

export interface AuthToken {
  token: string
  expiresAt: string
}
