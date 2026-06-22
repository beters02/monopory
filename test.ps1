$base = "http://localhost:5199"

$session = Invoke-RestMethod `
  -Method Post `
  -Uri "$base/auth/steam/session" `
  -ContentType "application/json" `
  -Body (@{
    steamId = 123456
    sessionTicket = "dev-ticket"
    displayName = "Tester"
  } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($session.accessToken)" }