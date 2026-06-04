module Shared.Grammar

open System

/// Convert snake_case to camelCase
let toCamelCase(s: string) =
    let parts = s.Split([| '_' |], StringSplitOptions.RemoveEmptyEntries)

    match parts with
    | [||] -> ""
    | _ ->
        let first = parts[0]
        let firstPart = $"{Char.ToLowerInvariant first[0]}{first[1..]}"

        let restParts =
            parts[1..]
            |> Array.map(fun part ->
                if part.Length = 0 then
                    ""
                else
                    $"{Char.ToUpperInvariant part[0]}{part[1..]}"
            )

        String.concat "" (Array.append [| firstPart |] restParts)

/// Convert PascalCase to snake_case
let toSnakeCase(s: string) =
    s
    |> Seq.fold
        (fun a c ->
            let l = c |> Char.ToLowerInvariant |> string

            if a = "" then
                l
            else
                let last = a[a.Length - 1]

                if Char.IsUpper c && (Char.IsLower last || Char.IsDigit last) then
                    $"{a}_{l}"
                else
                    $"{a}{l}"
        )
        ""

/// Convert snake_case to PascalCase
let toPascalCase(s: string) =
    s.Split([| '_' |], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map(fun part ->
        if part.Length = 0 then
            ""
        else
            $"{Char.ToUpperInvariant part[0]}{part[1..]}"
    )
    |> String.concat ""

/// Pluralize table names
let pluralise(word: string) : string =
    match word with
    | w when w.EndsWith "y" && w.Length > 1 && not("aeiouAEIOU".Contains(w[w.Length - 2])) -> w.Substring(0, w.Length - 1) + "ies"
    | w when w.EndsWith "s" || w.EndsWith "x" || w.EndsWith "ch" || w.EndsWith "sh" -> w + "es"
    | w -> w + "s"
