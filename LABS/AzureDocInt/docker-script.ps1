docker run `
   -d `
   --name sqlserver2022 `
   --hostname sqlserver2022 `
   -p 1434:1433 `
   -v sqlserver2022-data:/var/opt/mssql `
   -e "ACCEPT_EULA=Y" `
   -e "MSSQL_SA_PASSWORD=Corso2025" `
   mcr.microsoft.com/mssql/server:2022-latest