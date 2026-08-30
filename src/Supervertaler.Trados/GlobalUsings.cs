// The shared Supervertaler code moved out of Supervertaler.Trados.Core /
// .Models / .Settings and into Supervertaler.Core (the core/ submodule,
// compiled in via Supervertaler.Core.props).
//
// Forty-six files in this plugin use those types, most from inside the
// namespaces the types used to occupy, so they referenced them without a using
// at all. Rather than add an import to all forty-six, they are global here.
//
// Requires LangVersion latest in the .csproj: global usings are a C# 10
// compiler feature and this project targets net48, whose default is 7.3.

global using Supervertaler.Core;
global using Supervertaler.Core.Models;
