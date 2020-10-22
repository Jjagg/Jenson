# Jenson

Hacked together C# Source Generator that writes JSON converters for System.Text.Json.
The project is poorly structured and probably full of bugs, since I really just wrote what I needed. 
Though I'm, open to improvements and more features! Please open an issue if you'd like to contribute.

Three features I needed and have added:

- Property ordering for serialization.
- Conditional ignore for serialization of property (instance function name passed in attribute).
- Type discrimination based on a property value. User provides property name and a function that gives the actual type
given the value for the property in the JSON.

Those two last ones are both just string-based, so no compile-time safety or anything and error reporting is non-existent (though the C# compiler will help out a little).

I don't expect anyone to use this at this point, but if you're interested check out the tests or open an issue.

