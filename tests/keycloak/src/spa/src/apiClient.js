export async function fetchMe(apiBaseUrl, accessToken) {
  const response = await fetch(`${apiBaseUrl}/me`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })

  if (!response.ok) {
    throw new Error(`GET /me failed with status ${response.status}`)
  }

  return response.json()
}
