import asyncio
import base64
from datetime import datetime, timedelta
import json
import os
import re
import stat
from pathlib import Path
from typing import Any
import openpyxl
import pandas as pd
from playwright.async_api import async_playwright

JAMEA_URL = "https://beta.jameasaifiyah.org/"
API_BASE = "https://api.jameasaifiyah.org"

if "JAMEA_DATA_DIR" in os.environ:
    DATA_DIR = Path(os.environ["JAMEA_DATA_DIR"])
else:
    repo_data_dir = Path(__file__).resolve().parent.parent / "data"
    # Check if parent is a repository or writable workspace
    if repo_data_dir.exists() or not str(Path(__file__)).endswith(".app/Contents/Resources/helper/jamea_helper.py"):
        DATA_DIR = repo_data_dir
    else:
        DATA_DIR = Path.home() / "Library" / "Application Support" / "Jadwal" / "data"

DATA_DIR.mkdir(parents=True, exist_ok=True)

TOKEN_FILE = DATA_DIR / "jamea_token.json"
BROWSER_PROFILE = DATA_DIR / "browser-profile"

def save_token(token: str) -> None:
    """
    Save the Jamea JWT locally for reuse between runs.

    This is suitable for a prototype. For the final macOS app,
    store the token in Keychain instead of a normal file.
    """

    with open(TOKEN_FILE, "w", encoding="utf-8") as f:
        json.dump(
            {
                "access_token": token
            },
            f
        )

    # Restrict the file to the current user.
    os.chmod(TOKEN_FILE, stat.S_IRUSR | stat.S_IWUSR)


def load_token() -> str | None:
    """
    Load a previously saved Jamea JWT.
    """

    if not TOKEN_FILE.exists():
        return None

    try:
        with open(TOKEN_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)

        token = data.get("access_token")

        if isinstance(token, str) and token:
            return token

    except (OSError, json.JSONDecodeError):
        pass

    return None


def delete_token() -> None:
    """
    Delete the saved Jamea JWT when expired or invalid.
    """
    if TOKEN_FILE.exists():
        try:
            TOKEN_FILE.unlink()
        except OSError:
            pass
async def get_token(page) -> str:
    """
    Read the Jamea JWT inside the already-authenticated browser page.

    The token is never printed or saved to disk.
    """
    token = await page.evaluate("""
        () => sessionStorage.getItem("webauth_token_capture")
    """)

    if not token:
        raise RuntimeError(
            "Jamea authentication token was not found. "
            "Please log in through the browser first."
        )

    return token


async def api_fetch(page, url: str, body: dict) -> Any:
    """
    Make an authenticated API request from inside the Jamea page context.

    This keeps authentication inside the browser session.
    """
    result = await page.evaluate(
        """
        async ({ url, body }) => {
            const token =
                sessionStorage.getItem("webauth_token_capture");

            if (!token) {
                throw new Error("Authentication token not found.");
            }

            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Accept": "application/json, text/plain, */*",
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`,
                    "X-Menu-Id": "1372"
                },
                body: JSON.stringify(body)
            });

            const text = await response.text();

            let data;

            try {
                data = JSON.parse(text);
            } catch {
                data = text;
            }

            return {
                ok: response.ok,
                status: response.status,
                data
            };
        }
        """,
        {"url": url, "body": body},
    )

    if not result["ok"]:
        if result["status"] == 401:
            raise RuntimeError("AUTH_EXPIRED")

        raise RuntimeError(
            f"API request failed: {result['status']} "
            f"{result['data']}"
        )

    return result["data"]


async def get_user_info_from_token(page) -> dict:
    """
    Decode JWT claims inside the browser.

    This is only used to discover the student's own class,
    branch and academic year.
    """
    result = await page.evaluate("""
        () => {
            const token =
                sessionStorage.getItem("webauth_token_capture");

            if (!token) {
                throw new Error("JWT not found.");
            }

            const parts = token.split(".");

            if (parts.length !== 3) {
                throw new Error("Unexpected JWT format.");
            }

            const payload = parts[1];

            const normalized =
                payload.replace(/-/g, "+").replace(/_/g, "/");

            const json = decodeURIComponent(
                atob(normalized)
                    .split("")
                    .map(
                        c =>
                            "%" +
                            ("00" + c.charCodeAt(0).toString(16))
                                .slice(-2)
                    )
                    .join("")
            );

            return JSON.parse(json);
        }
    """)

    return result


async def get_current_week(page, year_ar: int, branch_id: str):
    url = (
        f"{API_BASE}/api/JadwalPage/"
        "SelectWeekDDList_JadwalReports"
    )

    data = await api_fetch(
        page,
        url,
        {
            "yearAR": year_ar,
            "branchID": branch_id,
            "teacherID": "%"
        }
    )

    weeks = data.get("data", [])

    current_week = next(
        (week for week in weeks if week.get("isCurrent")),
        None
    )

    if current_week is None:
        raise RuntimeError("Could not find current Jamea week.")

    return current_week


async def generate_excel(page, report_params: dict) -> Path:
    url = (
        f"{API_BASE}/api/JadwalReport/"
        "JadwalTimeTableReportExcel"
    )

    response = await api_fetch(
        page,
        url,
        report_params
    )

    if not response.get("succeeded"):
        raise RuntimeError(
            f"Excel generation failed: {response}"
        )

    # Current Jamea API response:
    #
    # {
    #   "succeeded": true,
    #   "data": {
    #       "downloadUrl": "...",
    #       "fileName": "..."
    #   }
    # }

    data = response.get("data")

    if not isinstance(data, dict):
        raise RuntimeError(
            f"Excel response has unexpected structure: {response}"
        )

    download_url = data.get("downloadUrl")
    filename = data.get("fileName")

    if not download_url:
        raise RuntimeError(
            f"Excel response did not contain downloadUrl: {response}"
        )

    if not filename:
        filename = Path(download_url).name

    print(f"Downloading: {filename}")
    print(f"URL: {download_url}")

    output_file = DATA_DIR / filename

    # Download directly through the authenticated page context.
    result = await page.evaluate(
        """
        async (url) => {
            const token =
                sessionStorage.getItem("webauth_token_capture");

            if (!token) {
                throw new Error("Authentication token not found.");
            }

            const response = await fetch(url, {
                method: "GET",
                headers: {
                    "Authorization": `Bearer ${token}`
                }
            });

            if (!response.ok) {
                const text = await response.text();

                throw new Error(
                    `Download failed: ${response.status} ${text}`
                );
            }

            const buffer = await response.arrayBuffer();

            let binary = "";

            const bytes = new Uint8Array(buffer);

            const chunkSize = 8192;

            for (
                let i = 0;
                i < bytes.length;
                i += chunkSize
            ) {
                binary += String.fromCharCode(
                    ...bytes.subarray(
                        i,
                        Math.min(i + chunkSize, bytes.length)
                    )
                );
            }

            return btoa(binary);
        }
        """,
        download_url
    )

    file_bytes = base64.b64decode(result)

    with open(output_file, "wb") as f:
        f.write(file_bytes)

    return output_file

ARABIC_DAYS = {
    "يوم الاثنين": "Monday",
    "يوم الثلاثاء": "Tuesday",
    "يوم الاربعاء": "Wednesday",
    "يوم الخميس": "Thursday",
    "يوم الجمعة": "Friday",
    "يوم السبت": "Saturday",
    "يوم الأحد": "Sunday",
}


def parse_time_range(value: str):
    """
    Convert:
        '08:50 - 09:25'

    into:
        ('08:50', '09:25')
    """

    if not isinstance(value, str):
        return None, None

    value = value.strip()

    match = re.match(
        r"^(\d{1,2}:\d{2})\s*-\s*(\d{1,2}:\d{2})$",
        value
    )

    if not match:
        return None, None

    return match.group(1), match.group(2)


def parse_week_dates(sheet):
    """
    Extract the week number and date range from row 23.

    Example:
        Week # 25
        [ 07 Sep 2026 - 13 Sep 2026 ]
    """

    week_number = None
    start_date = None
    end_date = None

    week_value = sheet["A23"].value
    date_value = sheet["B23"].value

    if isinstance(week_value, str):
        match = re.search(
            r"Week\s*#?\s*(\d+)",
            week_value,
            re.IGNORECASE
        )

        if match:
            week_number = int(match.group(1))

    if isinstance(date_value, str):
        match = re.search(
            r"\[\s*(\d{2}\s+\w+\s+\d{4})\s*-\s*(\d{2}\s+\w+\s+\d{4})\s*\]",
            date_value
        )

        if match:
            start_date = datetime.strptime(
                match.group(1),
                "%d %b %Y"
            ).date()

            end_date = datetime.strptime(
                match.group(2),
                "%d %b %Y"
            ).date()

    return week_number, start_date, end_date


def parse_timetable_excel(file_path):
    """
    Parse the Jamea timetable workbook into clean structured data.
    """

    workbook = openpyxl.load_workbook(
        file_path,
        data_only=True
    )

    sheet = workbook["Jadwal TimeTable"]

    # ---------------------------------------------------------
    # WEEK INFORMATION
    # ---------------------------------------------------------

    week_number, start_date, end_date = parse_week_dates(sheet)

    # ---------------------------------------------------------
    # PERIOD INFORMATION
    # ---------------------------------------------------------

    # Normal weekday timetable:
    # Row 6 = period names
    # Row 7 = times

    periods = []

    for column in range(2, sheet.max_column + 1):

        period_name = sheet.cell(6, column).value
        time_value = sheet.cell(7, column).value

        if not period_name:
            continue

        start_time, end_time = parse_time_range(time_value)

        # Ignore malformed periods
        if not start_time or not end_time:
            continue

        periods.append({
            "column": column,
            "period": str(period_name).strip(),
            "startTime": start_time,
            "endTime": end_time,
        })

    # ---------------------------------------------------------
    # DAYS
    # ---------------------------------------------------------

    entries = []

    # Monday-Friday
    day_rows = [
        (8, "Monday"),
        (10, "Tuesday"),
        (12, "Wednesday"),
        (14, "Thursday"),
        (16, "Friday"),
    ]

    for subject_row, english_day in day_rows:

        # Determine actual date for this day.
        date_value = None

        if start_date:
            weekday_offset = {
                "Monday": 0,
                "Tuesday": 1,
                "Wednesday": 2,
                "Thursday": 3,
                "Friday": 4,
            }[english_day]

            date_value = start_date + timedelta(
                days=weekday_offset
            )

        detail_row = subject_row + 1

        for period in periods:

            column = period["column"]

            subject = sheet.cell(
                subject_row,
                column
            ).value

            details = sheet.cell(
                detail_row,
                column
            ).value

            subject = (
                str(subject).strip()
                if subject is not None
                else ""
            )

            details = (
                str(details).strip()
                if details is not None
                else ""
            )

            # Empty subject = no class.
            if not subject:
                continue

            entries.append({
                "day": english_day,
                "date": (
                    date_value.isoformat()
                    if date_value
                    else None
                ),
                "period": period["period"],
                "startTime": period["startTime"],
                "endTime": period["endTime"],
                "subject": subject,
                "details": details,
            })

    # ---------------------------------------------------------
    # SATURDAY
    # ---------------------------------------------------------

    # Saturday has its own time row:
    # Row 18 = Time
    #
    # Subjects = row 19
    # Details = row 20

    saturday_periods = []

    for column in range(2, sheet.max_column + 1):

        time_value = sheet.cell(18, column).value

        start_time, end_time = parse_time_range(time_value)

        if not start_time or not end_time:
            continue

        saturday_periods.append({
            "column": column,
            "startTime": start_time,
            "endTime": end_time,
        })

    if start_date:
        saturday_date = start_date + timedelta(days=5)
    else:
        saturday_date = None

    for index, period in enumerate(saturday_periods, start=1):

        column = period["column"]

        subject = sheet.cell(
            19,
            column
        ).value

        details = sheet.cell(
            20,
            column
        ).value

        subject = (
            str(subject).strip()
            if subject is not None
            else ""
        )

        details = (
            str(details).strip()
            if details is not None
            else ""
        )

        if not subject:
            continue

        entries.append({
            "day": "Saturday",
            "date": (
                saturday_date.isoformat()
                if saturday_date
                else None
            ),
            "period": f"Period {index}",
            "startTime": period["startTime"],
            "endTime": period["endTime"],
            "subject": subject,
            "details": details,
        })

    return {
        "academicYear": "1447 / 1448",
        "weekNumber": week_number,
        "startDate": (
            start_date.isoformat()
            if start_date
            else None
        ),
        "endDate": (
            end_date.isoformat()
            if end_date
            else None
        ),
        "entries": entries,
    }


async def main():
    async with async_playwright() as p:

        print()
        print("======================================")
        print("JAMEA TIMETABLE HELPER")
        print("======================================")
        print()

        cached_token = load_token()

        if cached_token:
            print("Found saved Jamea authentication.")
            print("Testing saved session...")
        else:
            print("No saved authentication found.")
            print("Please log in through ITS.")

        context = await p.chromium.launch_persistent_context(
            user_data_dir=str(BROWSER_PROFILE),
            headless=False,
        )

        if cached_token:
            token_json = json.dumps(cached_token)

            await context.add_init_script(
                script=f"""
                (() => {{
                    try {{
                        const token = {token_json};

                        sessionStorage.setItem(
                            "webauth_token_capture",
                            token
                        );
                    }} catch (error) {{
                        console.error(
                            "Failed to restore Jamea authentication token:",
                            error
                        );
                    }}
                }})();
                """
            )

        page = context.pages[0] if context.pages else await context.new_page()

        await page.goto(JAMEA_URL)

        authenticated = False
        claims = None
        current_week = None

        if cached_token:
            try:
                claims = await get_user_info_from_token(page)
                branch_id = str(claims["branchID"])
                year_ar = int(claims["yearAR"])

                current_week = await get_current_week(
                    page,
                    year_ar,
                    branch_id
                )
                authenticated = True
                print("Saved session is valid!")
            except Exception as e:
                if str(e) == "AUTH_EXPIRED":
                    print("Saved session has expired (AUTH_EXPIRED).")
                else:
                    print(f"Saved session expired or invalid: {e}")
                delete_token()
                try:
                    await page.evaluate("() => sessionStorage.removeItem('webauth_token_capture')")
                    await page.goto(JAMEA_URL)
                except Exception:
                    pass
                print("Please log in through ITS.")

        if not authenticated:
            print()
            print("A browser window has been opened.")
            print("Please log in through ITS in the browser window.")
            print("Waiting for login to complete...")
            print()

            token = None
            for _ in range(120):  # Wait up to 120 seconds for user to log in
                await asyncio.sleep(1)
                try:
                    if page.is_closed():
                        raise RuntimeError("Browser window was closed before login completed.")
                    raw_token = await page.evaluate("""
                        () => sessionStorage.getItem("webauth_token_capture")
                    """)
                    if raw_token and isinstance(raw_token, str) and len(raw_token) > 20:
                        token = raw_token
                        break
                except Exception as e:
                    if "closed" in str(e).lower():
                        raise
                    continue

            if not token:
                raise RuntimeError("Login timed out. Please click Sync again and log in.")

            save_token(token)
            print("Jamea authentication detected.")

            claims = await get_user_info_from_token(page)

            branch_id = str(claims["branchID"])
            year_ar = int(claims["yearAR"])

            current_week = await get_current_week(
                page,
                year_ar,
                branch_id
            )

        branch_id = str(claims["branchID"])
        class_id = str(claims["classID"])
        year_ar = int(claims["yearAR"])

        print()
        print("Authenticated.")
        print(f"Branch: {branch_id}")
        print(f"Class:  {class_id}")
        print(f"Year:   {year_ar}")
        print()

        print(
            f"Current week: "
            f"{current_week['batchWeekName']} "
            f"({current_week['startDate']} → "
            f"{current_week['endDate']})"
        )

        report_params = {
            "yearAR": year_ar,
            "branchID": branch_id,
            "timeTablePeriodDayTypeID": 1,
            "batchWeeKNumber": current_week["batchWeekNumber"],
            "type": "Class",
            "classID": class_id,
            "teacherID": "%"
        }

        print()
        print("Generating timetable Excel...")

        excel_file = await generate_excel(
            page,
            report_params
        )

        print()
        print(f"Saved Excel:")
        print(excel_file)
        print()

        parsed = parse_timetable_excel(excel_file)

        json_file = DATA_DIR / "timetable.json"

        with open(
            json_file,
            "w",
            encoding="utf-8"
        ) as f:
            json.dump(
                parsed,
                f,
                ensure_ascii=False,
                indent=2
            )

        print(f"Saved clean timetable to: {json_file}")
        print(f"Timetable ready: {len(parsed['entries'])} periods for Week {parsed['weekNumber']} ({parsed['startDate']} → {parsed['endDate']})")

        await context.close()


if __name__ == "__main__":
    asyncio.run(main())