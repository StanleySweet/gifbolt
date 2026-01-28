#!/usr/bin/env python3
"""Analyze GifBolt frame dumps to diagnose frame disappearing issues."""

import os
import struct
from pathlib import Path

def analyze_dumps():
    temp_dir = os.environ.get("TEMP", "C:\\Windows\\Temp")
    frame_files = sorted(Path(temp_dir).glob("gifbolt_frame_*.txt"))
    raw_files = sorted(Path(temp_dir).glob("gifbolt_frame_*.raw"))
    
    print(f"Temp directory: {temp_dir}")
    print(f"Found {len(frame_files)} metadata files")
    print(f"Found {len(raw_files)} raw pixel files")
    print()
    
    if not frame_files:
        print("No frame dumps found. Make sure the app ran with the new DLL.")
        return
    
    # Analyze metadata
    print("=== Frame Sequence Analysis ===")
    frames = {}
    for meta_file in frame_files:
        try:
            with open(meta_file) as f:
                content = f.read()
                # Parse metadata
                frame_num = None
                displaying_alt = None
                for line in content.split('\n'):
                    if line.startswith('Frame:'):
                        frame_num = int(line.split(': ')[1].strip())
                    elif line.startswith('DisplayingAlt:'):
                        displaying_alt = line.split(': ')[1].strip() == 'true'
                
                if frame_num is not None:
                    frames[frame_num] = {
                        'meta': content,
                        'displaying_alt': displaying_alt,
                        'file': meta_file
                    }
                    print(f"Frame {frame_num:04d}: DisplayingAlt={displaying_alt}")
        except Exception as e:
            print(f"Error parsing {meta_file.name}: {e}")
    
    print()
    print("=== Frame Sequence Check ===")
    if frames:
        frame_nums = sorted(frames.keys())
        print(f"Frame 0 to {max(frame_nums)}")
        
        # Check for gaps
        gaps = []
        for i in range(len(frame_nums) - 1):
            if frame_nums[i+1] - frame_nums[i] != 1:
                gaps.append((frame_nums[i], frame_nums[i+1]))
        
        if gaps:
            print(f"⚠️  Found {len(gaps)} gaps in frame sequence:")
            for start, end in gaps:
                print(f"   Gap between frame {start} and {end} (missing {end-start-1} frames)")
        else:
            print(f"✓ No gaps in frames 0 to {max(frame_nums)}")
        
        # Check DisplayingAlt pattern
        print()
        print("=== Double-Buffering Pattern ===")
        for frame_num in sorted(frames.keys())[:10]:  # Show first 10
            alt = frames[frame_num]['displaying_alt']
            print(f"Frame {frame_num:04d}: Surface {'Alt' if alt else 'Primary '} being displayed")

if __name__ == "__main__":
    analyze_dumps()
