// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Saunter2.Extensions;

public static class ParameterInfoExtensions
{
    public static bool HasBindAsyncMethod(this ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        var method = type.GetMethod("BindAsync", BindingFlags.Public | BindingFlags.Static);
    
        if (method?.ReturnType != typeof(ValueTask<>) && 
            method?.ReturnType != typeof(ValueTask<>).MakeGenericType(type))
            return false;

        var parameters = method.GetParameters();
        return parameters.Length == 2 &&
               parameters[0].ParameterType == typeof(HttpContext) &&
               parameters[1].ParameterType == typeof(ParameterInfo);
    }
}
