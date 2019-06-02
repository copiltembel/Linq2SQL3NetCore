set dbName=L2S

sqlmetal /server:localhost /database:%dbName% /sprocs /timeout:30 /dbml:DataAccess.dbml /context:DbDataContext /namespace:Db.DataAccess.DataSet /pluralize /serialization:Unidirectional

"%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\Common7\IDE\texttransform.exe" -out DataAccess.cs -P "DataAccess.tt" -P "C:\Windows\Microsoft.NET\Framework" "DataAccess.tt"