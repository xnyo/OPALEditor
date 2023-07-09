# OPALEditor

Utility program to convert CSVs to opl files (Creation Kit's OPAL format), for the Skyrim SE Creation Kit.

Uses Mutagen to resolve form ids, thus it requires a valid installation of Skyrim SE to work.

## Usage

- Create a new sheet based off `template.csv`
- Populate it with the correct values
- Run the program to convert the `.csv` to `.opl`:

```
dotnet run -- mypalette.csv
```

- `mypalette.opl` will be created in the working directory

> **Tip:** You can output to a different folder (like CK's folder) with `-o`:
>
>   ```
>   dotnet run -- -o "C:\fast_vapore\Skyrim Special Edition\OPAL" mypalette.csv
>   ```

## LICENCE
MIT