using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace csParser
{
    /// <summary>
    /// Helper routines to parse the csharp AST (abstract syntax tree)
    /// </summary>
    class ParseHelper
    {
        public const string ReferencedMethodPrefix = "methodCalls:";
        /// <summary>
        /// Parse a single file and append results to the result builder
        /// the results building outside
        /// </summary>
        /// <param name="file"></param>
        /// <param name="argMap"></param>
        public static IList<OutputRecord> ProcessFile(string file, MultiMap<string, ArgMapRecord> argMap)
        {
            var results = new List<OutputRecord>();
            var text = File.ReadAllText(file);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            var declarations = from declaration in root.DescendantNodes()
                .OfType<VariableDeclarationSyntax>()
                select declaration;

            results.AddRange(ProcessDeclarations(text, file, declarations, argMap));

            var methodCalls = from declaration in root.DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>()
                //where methodDeclaration.Identifier.ValueText == "Main"
                select declaration;

            foreach (var methodCall in methodCalls)
            {
                var variable = methodCall.Expression.ToString();
                // Look up type of variable from declarations & prepend to method
                var decl = declarations
                    .FirstOrDefault(d => d.Variables.Any(v => v.Identifier.ValueText == variable));
                var type = decl != null ? decl.Type.ToString() : string.Empty;    
                var method = methodCall.Name.Identifier.Value;

                if (argMap.ContainsKey(methodCall.ToString()) || argMap.ContainsKey($"{type}.{method}"))
                {
                    //Console.WriteLine($"methodCall: {methodCall.ToString()}, {methodCall.Name.Identifier.Value}, {methodCall.Expression}, {methodCall.Parent}");
                    var parent = methodCall.Parent as InvocationExpressionSyntax;
                    var lineNumber = text.Substring(0, parent.Span.Start)?.Split('\n')?.Length;
                    var argMapVals = argMap[methodCall.ToString()] ?? argMap[$"{type}.{method}"];
                    results.AddRange(ProcessInvocationExpression(file, parent, argMapVals, lineNumber));
                }
            }

            /*var invocationExprs = from declaration in root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                select declaration;

            foreach (var invocationExpr in invocationExprs)
            {
                if (argMap.ContainsKey(invocationExpr.ToString()))
                {
                    //Console.WriteLine($"invocation: {invocationExpr.ToString()}");
                    var lineNumber = text.Substring(0, invocationExpr.Span.Start)?.Split('\n')?.Length;
                    results.AddRange(ProcessInvocationExpression(file, invocationExpr, argMap, lineNumber));
                }
            }*/

            return results;
        }

        public static Dictionary<string, string> GetVariables(string file)
        {
            var vars = new Dictionary<string, string>();
            var text = File.ReadAllText(file);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            var declarations = from declaration in root.DescendantNodes()
                    .OfType<VariableDeclarationSyntax>()
                select declaration;
            foreach (var declaration in declarations)
            {
                //Console.WriteLine($"declaration: {declaration}");
                foreach (var variableExpr in declaration.Variables)
                {
                    //Console.WriteLine($"type = {variableExpr.GetType().Name}, variableExpr.Parent.Parent type = {variableExpr.Parent?.Parent.GetType()}, variableExpr.Initializer.Value = {variableExpr.Initializer?.Value}, var.Initializer.Value.GetType() = {var.Initializer?.Value.GetType()}");
                    var variableName = variableExpr.Identifier.Text;
                    if (variableExpr.Initializer?.Value is LiteralExpressionSyntax)
                    {
                        var fqVariableName = GetFullyQualifiedVariableName(variableName, variableExpr);
                        var value = (variableExpr.Initializer.Value as LiteralExpressionSyntax).Token.ValueText;

                        if (!vars.ContainsKey(fqVariableName))
                        {
                            vars.Add(fqVariableName, value);
                        }
                    }
                }
            }

            return vars;
        }

        private static string GetFullyQualifiedVariableName(string variableName, SyntaxNode node)
        {
            if (node == null || node is NamespaceDeclarationSyntax)
            {
                return variableName;
            }

            var fqVariableName = variableName;
            if (node?.Parent is ClassDeclarationSyntax)
            {
                var classExpr = node.Parent as ClassDeclarationSyntax;
                fqVariableName = $"{classExpr.Identifier.ValueText}.{fqVariableName}";
            }

            return GetFullyQualifiedVariableName(fqVariableName, node.Parent);
        }

        /// <summary>
        /// Handle variable declarations
        /// </summary>
        /// <param name="text"></param>
        /// <param name="file"></param>
        /// <param name="declarations"></param>
        /// <param name="vars"></param>
        /// <param name="argMap"></param>
        /// <param name="resultBuilder"></param>
        private static IList<OutputRecord> ProcessDeclarations(
            string text, 
            string file, 
            IEnumerable<VariableDeclarationSyntax> declarations,
            MultiMap<string, ArgMapRecord> argMap)
        {
            var results = new List<OutputRecord>();
            foreach (var declaration in declarations)
            {
                //Console.WriteLine($"declaration: {declaration}");
                foreach (var var in declaration.Variables)
                {
                    //Console.WriteLine($"type = {var.GetType().Name}, var.Parent.Parent type = {var.Parent?.Parent.GetType()}, var.Initializer.Value = {var.Initializer?.Value}, var.Initializer.Value.GetType() = {var.Initializer?.Value.GetType()}");

                    if (var.Initializer?.Value is ObjectCreationExpressionSyntax)
                    {
                        var objectExpr = var.Initializer?.Value as ObjectCreationExpressionSyntax;
                        var objInitExprResults = ProcessObjectInitializerExpression(text, file, objectExpr, argMap);
                        if (objInitExprResults != null)
                        {
                            results.AddRange(objInitExprResults);
                        }
                    }
                    else if (var.Initializer?.Value is InvocationExpressionSyntax)
                    {
                        var invocationExpr = var.Initializer?.Value as InvocationExpressionSyntax;
                        var lineNumber = GetLineNumber(text, invocationExpr);
                        var argMapVals = argMap[invocationExpr.Expression.ToString()];
                        var invocationExprResults =
                            argMapVals != null
                            ? ProcessInvocationExpression(file, invocationExpr, argMapVals, lineNumber)
                            : null;
                        if (invocationExprResults != null)
                        {
                            results.AddRange(invocationExprResults);
                        }
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Handle object initialization calls
        /// </summary>
        /// <param name="text"></param>
        /// <param name="file"></param>
        /// <param name="resultBuilder"></param>
        /// <param name="objExpr"></param>
        /// <param name="vars"></param>
        /// <param name="argMap"></param>
        private static IList<OutputRecord> ProcessObjectInitializerExpression(
            string text, 
            string file,
            ObjectCreationExpressionSyntax objExpr, 
            MultiMap<string, ArgMapRecord> argMap)
        {
            var results = new List<OutputRecord>();
            //Console.WriteLine("In ProcessObjectInitializerExpression");
            if (objExpr.Type is IdentifierNameSyntax)
            {
                var idType = objExpr.Type as IdentifierNameSyntax;
                if (argMap.ContainsKey(idType.Identifier.ValueText))
                {
                    var argMapVals = argMap[idType.Identifier.ValueText];
                    // check for new SqlCommand(commandText,..)
                    foreach (var argMapVal in argMapVals)
                    {
                        var cmdIdx = argMapVal.ArgIdx;
                        if (objExpr.ArgumentList?.Arguments.Count >= cmdIdx + 1)
                        {
                            var procNameExpr = objExpr.ArgumentList?.Arguments[cmdIdx];
                            if (procNameExpr != null)
                            {
                                var lineNumber = GetLineNumber(text, procNameExpr);
                                if (procNameExpr?.Expression is LiteralExpressionSyntax)
                                {
                                    var procName = (procNameExpr.Expression as LiteralExpressionSyntax).Token.ValueText;
                                    results.Add(new OutputRecord(file, lineNumber, procName, false));
                                }
                                else if (procNameExpr?.Expression is IdentifierNameSyntax)
                                {
                                    // Walk up the AST looking for identifier
                                    var id = procNameExpr.Expression as IdentifierNameSyntax;
                                    string procName = FindIdentifierValue(id.Identifier.ValueText, procNameExpr.Parent);
                                    if (procName != null)
                                    {
                                        results.Add(new OutputRecord(file, lineNumber, procName, false));
                                    }
                                    else
                                    {
                                        results.Add(new OutputRecord(file, lineNumber, id.Identifier.ValueText,
                                            true));
                                    }
                                }
                                else
                                {
                                    results.Add(new OutputRecord(file, lineNumber,
                                        procNameExpr?.Expression.ToString(), true));
                                }
                            }
                        }
                    }

                    // Check if SqlCommand.CommandText is being set
                    if (objExpr?.Initializer != null)
                    {
                        foreach (var initExpr in objExpr.Initializer.Expressions)
                        {
                            //Console.WriteLine($"initExpr.GetType() = {initExpr.GetType()}");
                            if (initExpr is AssignmentExpressionSyntax)
                            {
                                var assignExpr = initExpr as AssignmentExpressionSyntax;
                                var left = assignExpr.Left as IdentifierNameSyntax;
                                var right = assignExpr.Right;
                                //Console.WriteLine($"right.GetType() = {right.GetType()}");
                                if (left.Identifier.ValueText == "CommandText")
                                {
                                    var lineNumber = GetLineNumber(text, assignExpr);
                                    string storedProcName = null;
                                    if (right is LiteralExpressionSyntax)
                                    {
                                        storedProcName = (right as LiteralExpressionSyntax).Token.ValueText;
                                    }
                                    else if (right is IdentifierNameSyntax)
                                    {
                                        var id = right as IdentifierNameSyntax;
                                        storedProcName = FindIdentifierValue(id.Identifier.ValueText, objExpr);
                                        if (storedProcName == null)
                                        {
                                            results.Add(new OutputRecord(file, lineNumber, id.Identifier.ValueText, true));
                                            Console.WriteLine(
                                                $"Failed to find identifier {id.Identifier.ValueText} value in {file}, line # {lineNumber}");
                                        }
                                    }
                                    else
                                    {
                                        storedProcName = right.ToString();
                                    }

                                    if (storedProcName != null)
                                    {
                                        results.Add(new OutputRecord(file, lineNumber, storedProcName, false));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Walk up the AST starting at the given node to find the identifier value
        /// </summary>
        /// <param name="identifierValueText"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static string FindIdentifierValue(string identifierValueText, SyntaxNode node)
        {
            if (node == null)
            {
                return null;
            }

            var blockExpr = node as BlockSyntax;
            if (blockExpr != null)
            {
                var declarations = 
                    from declaration in node.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                    select declaration;
                var vals = new StringBuilder();
                foreach (var variable in declarations)
                {
                    if (variable.Identifier.ValueText == identifierValueText && variable.Initializer != null)
                    {
                        vals.Append($"{variable.Initializer.Value.ToString()}|");
                    }
                }

                var assignments = 
                    from declaration in node.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                    select declaration;
                foreach (var assignment in assignments)
                {
                    if (assignment.Left.ToString() == identifierValueText)
                    {
                        if (assignment.Right is LiteralExpressionSyntax)
                        {
                            vals.Append($"{(assignment.Right as LiteralExpressionSyntax).Token.ValueText}|");
                        }
                        else if (assignment.Right is IdentifierNameSyntax)
                        {
                            var idVal = FindIdentifierValue((assignment.Right as IdentifierNameSyntax).Identifier.ValueText, node);
                            if (idVal != null)
                            {
                                vals.Append($"{idVal}|");
                            }
                        }
                    }
                }

                // remove trailing comma
                if (vals.Length > 0 && vals[vals.Length-1] == '|')
                {
                    vals.Remove(vals.Length - 1, 1);
                    return vals.ToString();
                }
            }

            var classDeclExpr = node as ClassDeclarationSyntax;
            if (classDeclExpr != null)
            {
                foreach (var memberExpr in classDeclExpr.Members)
                {
                    if (memberExpr is FieldDeclarationSyntax)
                    {
                        var fieldVal = GetFieldValue(memberExpr as FieldDeclarationSyntax, identifierValueText);
                        if (fieldVal != null)
                        {
                            return fieldVal;
                        }
                    }
                }
            }

            var fieldExpr = node as FieldDeclarationSyntax;
            if (fieldExpr != null)
            {
                var val = GetFieldValue(fieldExpr, identifierValueText);
                if (val != null)
                {
                    return val;
                }
            }

            var methodExpr = node as MethodDeclarationSyntax;
            if (methodExpr != null)
            {
                for(int i=0; i<methodExpr.ParameterList.Parameters.Count; i++)
                {
                    var parameterExpr = methodExpr.ParameterList.Parameters[i];
                    if (parameterExpr.Identifier.ValueText == identifierValueText)
                    {
                        var ns = GetNameSpace(methodExpr);
                        var nsSuffix = ns != null ? $":{ns}" : "";
                        // Need to get all the method calls for this method
                        var classNames = GetClassNames(methodExpr);
                        return $"{ReferencedMethodPrefix}{classNames}.{methodExpr.Identifier.ValueText}:{i}{nsSuffix}";
                    }
                }
            }

            // Nothing found yet; go up one level
            return FindIdentifierValue(identifierValueText, node.Parent);
        }

        private static string GetClassNames(SyntaxNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var classExpr = node as ClassDeclarationSyntax;
            if (classExpr != null)
            {
                var className = classExpr.Identifier.ValueText;
                var interfaceName = classExpr.BaseList?.Types.Aggregate("",
                    (val, b) => (val != "" ? $"{val}|" : $"") + $"{b.Type.ToString()}");
                return className + (interfaceName != "" ? $"|{interfaceName}" : "");
            }

            return GetClassNames(node.Parent);
        }

        private static string GetFieldValue(FieldDeclarationSyntax fieldExpr, string identifierValueText)
        {
            var variableExpr = fieldExpr.Declaration;
            foreach (var variable in variableExpr.Variables)
            {
                if (variable.Identifier.ValueText == identifierValueText.Replace("this.",""))
                {
                    {
                        if (variable.Initializer.Value is LiteralExpressionSyntax)
                        {
                            var val = variable.Initializer.Value as LiteralExpressionSyntax;
                            return val.Token.ValueText;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Handle method calls
        /// </summary>
        /// <param name="text"></param>
        /// <param name="file"></param>
        /// <param name="resultBuilder"></param>
        /// <param name="invocationExpr"></param>
        /// <param name="argMapVals"></param>
        /// <param name="lineNumber"></param>
        /// <param name="vars"></param>
        private static IList<OutputRecord> ProcessInvocationExpression(
            string file, 
            InvocationExpressionSyntax invocationExpr, 
            //MultiMap<string, ArgMapRecord> argMap, 
            IEnumerable<ArgMapRecord> argMapVals,
            int? lineNumber)
        {
            var results = new List<OutputRecord>();
            if (invocationExpr != null)
            {
                /*if (!argMap.ContainsKey(invocationExpr.Expression.ToString()))
                {
                    return results;
                }
                var argMapVals = argMap[invocationExpr.Expression.ToString()];*/
                foreach (var argMapVal in argMapVals)
                {
                    var argIdx = argMapVal.ArgIdx;
                    var nameSpace = argMapVal.NameSpace;
                    if (!string.IsNullOrEmpty(nameSpace))
                    {
                        // If the namespace isn't referenced, then this is a different type
                        if (!IsNameSpaceReferenced(nameSpace, invocationExpr.Parent))
                        {
                            continue;
                        }
                    }

                    if (invocationExpr.ArgumentList.Arguments.Count >= argIdx + 1)
                    {
                        var arg = invocationExpr.ArgumentList.Arguments[argIdx];
                        var expr = arg.Expression;
                        // *** This should filter out polymorphic variants where the argument at the
                        // specified position is of type CommandType ***
                        // 
                        string procName = null;
                        if (expr is IdentifierNameSyntax)
                        {
                            var id = expr as IdentifierNameSyntax;
                            procName = FindIdentifierValue(id.Identifier.ValueText, invocationExpr.Parent);
                            
                            if (procName != null)
                            {
                                results.Add(new OutputRecord(file, lineNumber ?? 0, procName, false));
                            }
                            else
                            {
                                results.Add(new OutputRecord(file, lineNumber ?? 0, id.Identifier.ValueText, true));
                                Console.WriteLine(
                                    $"Failed to find identifier {id.Identifier.ValueText} value in {file}, line # {lineNumber ?? 0}");
                            }
                        }
                        else if (expr is MemberAccessExpressionSyntax)
                        {
                            var id = expr as MemberAccessExpressionSyntax;

                            if (id.Expression is IdentifierNameSyntax)
                            {
                                var ident = id.Expression as IdentifierNameSyntax;
                                if (ident.Identifier.ValueText == "CommandType")
                                {
                                    continue;
                                }
                            }

                            procName = FindIdentifierValue(id.ToString(), invocationExpr.Parent);

                            if (procName != null)
                            {
                                results.Add(new OutputRecord(file, lineNumber ?? 0, procName, false));
                            }
                            else
                            {
                                results.Add(new OutputRecord(file, lineNumber ?? 0, id.ToString(), true));
                                Console.WriteLine(
                                    $"Failed to find identifier {id.ToString()} value in {file}, line # {lineNumber ?? 0}");
                            }
                        }
                        else if (expr is LiteralExpressionSyntax)
                        {
                            procName = (expr as LiteralExpressionSyntax).Token.ValueText;
                            results.Add(new OutputRecord(file, lineNumber ?? 0, procName, false));
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Is there a usings statement for the given namespace?
        /// </summary>
        /// <param name="nameSpace"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static bool IsNameSpaceReferenced(string nameSpace, SyntaxNode node)
        {
            if (node == null)
            {
                return false;
            }
            bool isNameSpaceReferenced = false;
            if (node is CompilationUnitSyntax)
            {
                var compilationUnit = node as CompilationUnitSyntax;
                isNameSpaceReferenced = compilationUnit.Usings.Any(n => n.Name.ToString() == nameSpace);
            }

            if (node is NamespaceDeclarationSyntax)
            {
                var nameSpaceExpr = node as NamespaceDeclarationSyntax;
                if (nameSpaceExpr.Name.ToString() == nameSpace)
                {
                    isNameSpaceReferenced = true;
                }
            }

            return isNameSpaceReferenced || IsNameSpaceReferenced(nameSpace, node.Parent);
        }

        private static string GetNameSpace(SyntaxNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node is NamespaceDeclarationSyntax)
            {
                return (node as NamespaceDeclarationSyntax).Name.ToString();
            }

            return GetNameSpace(node.Parent);
        }

        private static int GetLineNumber(string text, SyntaxNode expr)
        {
            return expr != null ? text.Substring(0, expr.Span.Start).Split('\n').Length : 0;
        }
    }
}
