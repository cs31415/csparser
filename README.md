# CS Parser

Utility to parse a directory containing C# code and extract stored procedure/SQL calls. This utility uses the Roslyn code analysis APIs.

### Usage / Installation

Clone git repo on local machine.

Edit file `ArgMap.txt` in the project directory if required with the method names and argument index of the SQL command text to be extracted.  

For e.g.

```
SqlHelper.ExecuteDataset,1,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteReader,1,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteNonQuery,1,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteScalar,1,Microsoft.ApplicationBlocks.Data

SqlHelper.ExecuteDataset,2,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteReader,2,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteNonQuery,2,Microsoft.ApplicationBlocks.Data
SqlHelper.ExecuteScalar,2,Microsoft.ApplicationBlocks.Data
SqlCommand,0
```

This is in the format:

```
Method name (if static include the class name), argument index, namespace of the referenced class
```
Methods with the same name in different namespaces can be disambiguated by specifying the namespace name.


Open command prompt, cd to debug/release directory and run the following command:

```
csparser C:\development\testApp\src *.cs
```

where `C:\development\testApp\src` is the directory containing C# code to be parsed.


The results are written to a `storedprocs.csv` file in the executable directory with the following format:


|File|LineNumber|CommandText|IsVariable|ErrorMsg|
|---|---|---|---|---|
|C:\development\testApp\src\Application\Core\DatabaseHelper.cs|531|usp_site_get_loan_data|False||
|C:\development\testApp\src\Application\Core\DatabaseHelper.cs|814|Loan_Search|False||
|C:\development\testApp\src\Application\Core\DatabaseHelper.cs|850|Loan_Update|False||
|C:\development\testApp\src\Application\Core\DatabaseHelper.cs|907|Loan_Create|False||


*Columns:*
```
File - The file being parsed
LineNumber - Line number where SQL call found
CommandText - SQL stored procedure or command being invoked
IsVariable - If true, indicates that CommandText is a variable and prints the variable name (this might be subsequently resolved to an actual SQL command)
ErrorMsg - Any parsing error message(s)
```


### Dependencies
**Nuget packages:**  
Microsoft.CodeAnalysis.CSharp  
Microsoft.CodeAnalysis.CSharp.Scripting    
Microsoft.CodeAnalysis.Common  
Microsoft.CodeAnalysis.Analyzers  
Microsoft.CodeAnalysis.Scripting.Common  
Microsoft.CSharp  

### Prerequisites
.NET Framework 4.6.1 or greater

 
### Examples

Some examples of the SQL commands that the `ArgMap.txt` provided above extracts:

1. 
```
SqlCommand cmd = new SqlCommand("Loan_Search", conn);

```

In this case, `Loan_Search` will be extracted.


2. It also looks up constant identifiers. For e.g.

```
internal static class Constants
{
	public static class Loan
	{
		public static class Procedures
		{
			#region Constants
			/// <summary>
			/// Constant for procedure 'Loan_Update'
			/// </summary>
			public static readonly string LOAN_UPDATE = "Loan_Update";
		}
	}	
}

using (SqlCommand cmd = new SqlCommand(Constants.Loan.Procedures.LOAN_UPDATE, conn))
{
...
}
```

In this case, `Loan_Update` will be extracted.


3. It will also parse upto one level of indirection - i.e. if the method calls in the config are wrapped in another function, then looking up references to the wrapped function.


```
using (SqlCommand cmd = GetSqlCommand(conn, "Customer_Merge", prams))
{
}

private SqlCommand GetSqlCommand(IDbConnection sqlConnection, string commandText, params IDataParameter[] commandParameters)
{
	var sqlCommand = new SqlCommand
	{
		CommandText = commandText,
		CommandType = CommandType.StoredProcedure,
		Connection = sqlConnection as SqlConnection
	};

	if (commandParameters != null)
	{
		sqlCommand.Parameters.AddRange(commandParameters);
	}

	return sqlCommand;
}
```		

In this case, `Customer_Merge` would be extracted.


4. It also looks up object initializer expressions:


```
private const string DOCUMENT_LIST_MERGE = "Document_List_Merge";

public static int SaveDocuments(List<Document> lists)
{
	DataTable table = ConvertDocumentsToDataTable(lists);

	using (SqlConnection conn = new SqlConnection(ConfigReader.ConnectionString))
	{
		conn.Open();

		using (
			SqlCommand cmd = new SqlCommand
				{
					CommandType = CommandType.StoredProcedure,
					CommandText = DOCUMENT_LIST_MERGE,
					Connection = conn
				})
		{
			cmd.Parameters.Add("@lists", SqlDbType.Structured).Value = table;
			return cmd.ExecuteNonQuery();
		}
	}
}
```

In this case, `Document_List_Merge` would be extracted.