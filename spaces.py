from tkinter import Tk
import pyautogui
from pyautogui import position, hotkey, moveTo
from os import system
from os.path import exists, getmtime
from pickle import load
import time
from threading import Thread

pyautogui.FAILSAFE = False

def main(monitor_width, monitor_height, defaults):

    # mainloop
    while True:
        # run a worker to listen for hotcorners
        stop_threads = False
        worker_thread = Thread(target=worker, args=(monitor_width, monitor_height, defaults, lambda: stop_threads))
        worker_thread.start()

        # listen for defaults settings update
        init_st_mtime = getmtime("spaces.pkl")
        curr_st_mtime = init_st_mtime

        while init_st_mtime == curr_st_mtime:
            curr_st_mtime = getmtime("spaces.pkl")
            time.sleep(0.2)

        print("Settings updated!")

        # breaks out after file update
        stop_threads = True
        worker_thread.join()

        # reset defaults
        defaults = load_defaults()

def worker(monitor_width, monitor_height, defaults, stop):
    default_topleft, default_topright, default_bottomleft, default_bottomright = [item for key, item in defaults.items()]   # extract items from defaults
    hotcorners = hot(monitor_width, monitor_height, default_topleft, default_topright, default_bottomleft, default_bottomright)

    while not stop():
        hotcorners.tick()

class hot(object):
    def __init__(self, monitor_width, monitor_height, default_topleft, default_topright, default_bottomleft, default_bottomright):
        self.monitor_width = monitor_width
        self.monitor_height = monitor_height
        self.topleft = default_topleft
        self.topright = default_topright
        self.bottomleft = default_bottomleft
        self.bottomright = default_bottomright

        self.prev = None

    def tick(self):
        x, y = position()

        if self.prev and (x, y) == self.prev:
            return  # skip if mouse remains in corner
        else:
            self.prev = (x, y)

        if (x, y) == (0, 0):
            self.topleft(monitor_width, monitor_height)
        elif (x, y) == (self.monitor_width-1, 0):
            self.topright(monitor_width, monitor_height)
        elif (x, y) == (0, self.monitor_height-1):
            self.bottomleft(monitor_width, monitor_height)
        elif (x, y) == (self.monitor_width-1, self.monitor_height-1):
            self.bottomright(monitor_width, monitor_height)

def show_desktop(*args):
    hotkey("win", "d")

def show_windows(*args):
    hotkey("win", "tab")

def sleep(monitor_width, monitor_height, *args):
    x, y = int(monitor_width/2), int(monitor_height/2)
    moveTo(x, y)    # move mouse to center of screen, so screen doesn't lock instantly on awaken
    system("rundll32.exe powrprof.dll,SetSuspendState 0,1,0")

def screen_lock(monitor_width, monitor_height, *args):
    x, y = int(monitor_width/2), int(monitor_height/2)
    moveTo(x, y)    # move mouse to center of screen, so screen doesn't lock instantly on awaken
    system("rundll32.exe user32.dll,LockWorkStation")

def show_start(*args):
    hotkey("win")

def load_defaults():
    with open("spaces.pkl", 'rb') as file:
        defaults_list = load(file)

        print(defaults_list)

    menu = ["Show desktop", "Show start menu", "Sleep", "Lock screen", "Show applications"]
    menu_functions = [show_desktop, show_start, sleep, screen_lock, show_windows]

    default_topleft     = menu_functions[menu.index(defaults_list[0])]
    default_topright    = menu_functions[menu.index(defaults_list[1])]
    default_bottomleft  = menu_functions[menu.index(defaults_list[2])]
    default_bottomright = menu_functions[menu.index(defaults_list[3])]

    defaults = {
        defaults_list[0]: default_topleft,
        defaults_list[1]: default_topright,
        defaults_list[2]: default_bottomleft,
        defaults_list[3]: default_bottomright,
    }

    return defaults

if __name__ == "__main__":
    # initialise defaults
    root = Tk()
    
    if exists("spaces.pkl"):
        defaults = load_defaults()

    else:
        defaults = {
            "Show applications":show_windows,
            "Show desktop":show_desktop,
            "Show start menu":show_start,
            "Lock screen":screen_lock,
        }

    monitor_height = root.winfo_screenheight()
    monitor_width = root.winfo_screenwidth()

    main(monitor_width, monitor_height, defaults)   # silent operation, for execution from startup