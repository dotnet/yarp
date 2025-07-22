// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Threading.Tasks;

var client = new HttpClient();

string[] planets = { "Mercury", "Venus", "Earth", "Mars", "Jupiter" };

foreach (var planet in planets)
{
    _ = Task.Run(() =>
    {
        CreateLoad("http://localhost:5000/" + planet);
    });
}
var c = Console.ReadKey();

async void CreateLoad(string UrlPrefix)
{
    var i = 0;
    while (true)
    {
        var url = UrlPrefix + "/" + i;
        var resp = await client.GetAsync(url);
        var result = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"Requested: {url}, Result: {resp.StatusCode}, Length: {result.Length}");
        i++;
    }
}
