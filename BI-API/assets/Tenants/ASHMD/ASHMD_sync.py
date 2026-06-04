import os
import subprocess
import sys

# Windows Scheduler Legacy Wrapper
# Instead of doing SQLite operations in Python, we call the transaction-safe .NET sync CLI engine.

def run_dotnet_sync():
    current_dir = os.path.dirname(os.path.abspath(__file__))
    company_id = os.path.basename(current_dir).upper()
    
    # Resolve backend project path
    backend_dir = None
    curr = current_dir
    while curr:
        if os.path.exists(os.path.join(curr, "BimasaktiReports.csproj")):
            backend_dir = curr
            break
        parent = os.path.dirname(curr)
        if parent == curr:
            break
        curr = parent

    if not backend_dir:
        print("Error: Could not find BimasaktiReports backend folder.")
        sys.exit(1)

    print(f"Triggering standalone C# sync engine for tenant: {company_id}...")
    try:
        # Run C# standalone command line synchronizer
        res = subprocess.run(
            ["dotnet", "run", "--project", os.path.join(backend_dir, "BimasaktiReports.csproj"), "--", "--sync", company_id],
            capture_output=True,
            text=True
        )
        print(res.stdout)
        if res.stderr:
            print(res.stderr, file=sys.stderr)
        sys.exit(res.returncode)
    except Exception as e:
        print(f"Critical error executing C# sync: {str(e)}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    run_dotnet_sync()
