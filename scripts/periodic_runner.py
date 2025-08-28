#!/usr/bin/env python3
"""
Periodic Command Runner

Executes a given command at specified intervals until interrupted.
Provides real-time feedback and execution summaries.

Usage:
    python3 periodic_runner.py "command to run" --interval 30
    python3 periodic_runner.py "dotnet run --project ./samples/ConsoleAppTesting/" --interval 30
"""

import argparse
import subprocess
import time
import signal
import sys
from datetime import datetime, timedelta
from typing import Optional


class PeriodicRunner:
    def __init__(self, command: str, interval: int):
        self.command = command
        self.interval = interval
        self.execution_count = 0
        self.success_count = 0
        self.failure_count = 0
        self.start_time = datetime.now()
        self.running = True
        
        # Setup signal handlers for graceful shutdown
        signal.signal(signal.SIGINT, self._signal_handler)
        signal.signal(signal.SIGTERM, self._signal_handler)
    
    def _signal_handler(self, signum, frame):
        """Handle interrupt signals gracefully"""
        print(f"\n\n🛑 Received signal {signum}. Shutting down gracefully...")
        self.running = False
    
    def _execute_command(self) -> bool:
        """Execute the command and return success status"""
        print(f"🚀 Executing: {self.command}")
        execution_start = datetime.now()
        
        try:
            result = subprocess.run(
                self.command,
                shell=True,
                capture_output=False,  # Show output in real-time
                text=True
            )
            
            execution_time = datetime.now() - execution_start
            success = result.returncode == 0
            
            status_emoji = "✅" if success else "❌"
            print(f"{status_emoji} Command completed in {execution_time.total_seconds():.2f}s (exit code: {result.returncode})")
            
            return success
            
        except Exception as e:
            execution_time = datetime.now() - execution_start
            print(f"❌ Command failed with exception after {execution_time.total_seconds():.2f}s: {e}")
            return False
    
    def _print_summary(self, next_run: Optional[datetime] = None):
        """Print execution summary"""
        current_time = datetime.now()
        total_runtime = current_time - self.start_time
        
        print(f"\n{'='*60}")
        print(f"📊 EXECUTION SUMMARY")
        print(f"{'='*60}")
        print(f"Command: {self.command}")
        print(f"Interval: {self.interval} seconds")
        print(f"Total executions: {self.execution_count}")
        print(f"Successful: {self.success_count} ✅")
        print(f"Failed: {self.failure_count} ❌")
        
        if self.execution_count > 0:
            success_rate = (self.success_count / self.execution_count) * 100
            print(f"Success rate: {success_rate:.1f}%")
        
        print(f"Total runtime: {total_runtime}")
        
        if next_run:
            time_until_next = next_run - current_time
            print(f"Next execution: {next_run.strftime('%H:%M:%S')} (in {time_until_next.total_seconds():.0f}s)")
        
        print(f"{'='*60}")
    
    def run(self):
        """Main execution loop"""
        print(f"🔄 Starting periodic execution of: {self.command}")
        print(f"⏱️  Interval: {self.interval} seconds")
        print(f"🕐 Started at: {self.start_time.strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"💡 Press Ctrl+C to stop\n")
        
        while self.running:
            # Execute command
            self.execution_count += 1
            success = self._execute_command()
            
            if success:
                self.success_count += 1
            else:
                self.failure_count += 1
            
            if not self.running:
                break
                
            # Calculate next run time
            next_run = datetime.now() + timedelta(seconds=self.interval)
            self._print_summary(next_run)
            
            # Wait for next execution
            print(f"\n💤 Waiting {self.interval} seconds until next execution...")
            
            # Sleep in small intervals to allow for responsive interruption
            sleep_remaining = self.interval
            while sleep_remaining > 0 and self.running:
                sleep_time = min(1, sleep_remaining)
                time.sleep(sleep_time)
                sleep_remaining -= sleep_time
        
        # Final summary
        print(f"\n🏁 Execution stopped.")
        self._print_summary()


def main():
    parser = argparse.ArgumentParser(
        description="Execute a command periodically at specified intervals",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python3 periodic_runner.py "echo Hello World" --interval 10
  python3 periodic_runner.py "dotnet run --project ./samples/ConsoleAppTesting/" --interval 30
  python3 periodic_runner.py "curl -s https://example.com > /dev/null" --interval 60
        """
    )
    
    parser.add_argument(
        "command",
        help="Command to execute periodically"
    )
    
    parser.add_argument(
        "--interval", "-i",
        type=int,
        default=30,
        help="Interval between executions in seconds (default: 30)"
    )
    
    parser.add_argument(
        "--version",
        action="version",
        version="Periodic Runner 1.0.0"
    )
    
    args = parser.parse_args()
    
    if args.interval <= 0:
        print("❌ Error: Interval must be greater than 0 seconds")
        sys.exit(1)
    
    runner = PeriodicRunner(args.command, args.interval)
    
    try:
        runner.run()
    except KeyboardInterrupt:
        print("\n\n🛑 Interrupted by user")
    except Exception as e:
        print(f"\n\n💥 Unexpected error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
