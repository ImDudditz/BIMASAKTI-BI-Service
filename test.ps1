[Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
$response = Invoke-WebRequest -Uri 'https://localhost:8889/BI-SERVICE/BMS_Core_IIS/api/auth/login' -Method Post -Body '{"username":"admin","password":"password","company_id":"ashmd"}' -ContentType 'application/json' -SkipHttpError
$response.Headers
$response.StatusCode
$response.Content
