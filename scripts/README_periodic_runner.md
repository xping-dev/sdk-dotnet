# Periodic Runner

A Python script that executes commands at specified intervals with detailed logging and statistics.

## Features

- ✅ Execute any command periodically
- 📊 Real-time execution statistics
- 🕐 Next execution countdown
- 🛑 Graceful shutdown with Ctrl+C
- 💯 Success/failure tracking
- ⏱️ Execution timing

## Usage

### Basic Usage
```bash
python3 periodic_runner.py "command to run" --interval 30
```

### Examples

Execute the Xping console app every 30 seconds:
```bash
python3 periodic_runner.py "dotnet run --project ./samples/ConsoleAppTesting/" --interval 30
```

Check website availability every 60 seconds:
```bash
python3 periodic_runner.py "curl -s https://example.com > /dev/null" --interval 60
```

Simple test with date command every 10 seconds:
```bash
python3 periodic_runner.py "date" --interval 10
```

Run a health check every 2 minutes:
```bash
python3 periodic_runner.py "dotnet test tests/Xping.Sdk.Core.UnitTests/" --interval 120
```

### Options

- `--interval` or `-i`: Interval between executions in seconds (default: 30)
- `--help`: Show help message
- `--version`: Show version information

## Output

The script provides:
- Real-time command output
- Execution status (✅ success / ❌ failure)
- Execution timing
- Running statistics summary
- Next execution countdown

## Stopping

Press `Ctrl+C` to stop the script gracefully. It will show a final summary before exiting.

## Requirements

- Python 3.6+
- No additional dependencies required (uses only standard library)
