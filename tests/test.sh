#!/bin/zsh


# Check if two arguments are given
if [ $# -ne 2 ]; then
    echo "Usage: $0 <sln_dir> <unity_scripts_dir>"
    exit 1
fi

# Convert relative paths to absolute paths
sln_dir=$(greadlink -f "$1")
unity_scripts_dir=$(greadlink -f "$2")

dotnet build "$sln_dir"


# Create the target directory if it does not exist
mkdir -p "$unity_scripts_dir"

# Loop over all .cs files in the source directory
for dir in "$sln_dir"/*/
do
    if [[ $dir == *".test/" ]]; then
        continue
    else 
        /Users/bare/RiderProjects/Solnet.Anchor/Solnet.Anchor.Tool/bin/Debug/net6.0/Solnet.Anchor.Tool -i ./target/idl/*.json -o "$dir"/Idl.cs;
    fi

    # Get the base name of the file
    base_name=$(basename "$file")
    
    # Apply sed to the file and write the result to the target directory
    sed 's/Solnet/Solana.Unity/g' "$file" > "$unity_scripts_dir/$base_name"
done

dotnet test -l "console;verbosity=detailed" "$sln_dir"