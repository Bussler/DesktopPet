# Third-party notices

## YamlDotNet 18.1.0

The application uses YamlDotNet for configuration parsing and serialization. Package metadata declares the MIT license. The source license is available at [YamlDotNet v18.1.0 LICENSE.txt](https://github.com/aaubry/YamlDotNet/blob/v18.1.0/LICENSE.txt).

Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013, 2014 Antoine Aubry and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Microsoft .NET runtime

The self-contained Windows publish includes Microsoft .NET and Windows Desktop runtime files. Their license and third-party notice files are available in the restored .NET runtime packages and Windows Desktop SDK; `dotnet publish` does not emit them into the app folder. The behavior-test-only MSTest dependencies are not part of the app publish.

The runtime is self-contained, so it does not receive updates from an installed .NET runtime. See Microsoft's [self-contained deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/) and [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) for servicing requirements.

The packaged Hood art is the project owner's work and is intended for personal use only; no external redistribution is planned.
