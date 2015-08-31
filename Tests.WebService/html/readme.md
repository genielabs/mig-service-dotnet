# MarkDown file test

**Hello** *world!*

## Just testing...

<!-- test comment... it will be shown in the HTML source code though --> 

[comment]: <> (This is a comment, it will not be included)

[//]: <> (This is also a comment.)

[//]: # (This may be the most platform independent comment)

[comment]: <> (Add a C# syntax highlighter in the HTML header file...)

Code test:
```csharp
public class MigClientRequest
{
    private object responseData;

    public readonly MigInterfaceCommand Command;
    public readonly object Context;

    public object ResponseData
    {
        get { return responseData; }
        set
        {
            Handled = true;
            responseData = value;
        }
    }

    public bool Handled = false;

    public MigClientRequest(MigInterfaceCommand command, object context)
    {
        Command = command;
        Context = context;
    }
}
```