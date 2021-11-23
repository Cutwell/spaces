# Import the required libraries
from tkinter import *
from pystray import MenuItem as item
import pystray
from PIL import Image, ImageTk
from spaces import *
from threading import Thread
from os.path import exists
from pickle import load
import time

root = Tk()
root.iconbitmap('favicon.ico')
root.title('Spaces for Windows 10')
worker_thread_alpha = None
stop_threads_alpha = False

def quit_window(icon, item):
    icon.stop()
    root.destroy()

    stop_threads_alpha = True
    worker_thread_alpha.join()

def show_settings(icon, item):
    icon.stop()
    root.after(0, root.deiconify())

def hide_window():
    root.withdraw()
    image=Image.open("favicon.ico")
    menu=(item('Quit', quit_window), item('Settings', show_settings))
    icon=pystray.Icon("Spaces for Windows 10", image, "Spaces for Windows 10", menu)
    icon.run()

if __name__ == "__main__":
    if exists("spaces.pkl"):
        with open("spaces.pkl", 'rb') as file:
            gui_defaults = load(file)
        hot_defaults = load_defaults()
    else:
        gui_defaults = ["Show applications", "Show desktop", "Show start menu", "Lock screen"]
        hot_defaults = [show_windows, show_desktop, show_start, screen_lock]

    # initialise hotcorners worker
    monitor_height = root.winfo_screenheight()
    monitor_width = root.winfo_screenwidth()

    stop_threads_alpha = False
    worker_thread_alpha = Thread(target=hotcorners, args=(monitor_width, monitor_height, hot_defaults, lambda: stop_threads_alpha))
    worker_thread_alpha.start()

    # initialise gui
    gui(root, gui_defaults)

    # minimise to taskbar on window quit
    root.protocol('WM_DELETE_WINDOW', hide_window)

    hide_window()

    root.mainloop()