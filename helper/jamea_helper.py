import asyncio
import base64
from datetime import datetime, timedelta
import json
import os
import re
import stat
import time
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


def is_jwt_valid(token: str | None) -> bool:
    """
    Check if a JWT is well-formed and unexpired.
    Returns False if the token is None, malformed, or expired.
    """
    if not token or not isinstance(token, str):
        return False
    parts = token.split(".")
    if len(parts) != 3:
        return False
    try:
        payload = parts[1]
        payload += "=" * (-len(payload) % 4)
        claims = json.loads(base64.urlsafe_b64decode(payload))
        exp = claims.get("exp")
        if exp is not None and isinstance(exp, (int, float)):
            # Give a 60-second safety cushion
            if exp <= (time.time() + 60):
                return False
        return True
    except Exception:
        return False


def decode_jwt_claims(token: str) -> dict:
    """
    Decode JWT claims payload directly in Python.
    """
    parts = token.split(".")
    if len(parts) != 3:
        raise ValueError("Invalid JWT format.")
    payload = parts[1]
    payload += "=" * (-len(payload) % 4)
    return json.loads(base64.urlsafe_b64decode(payload))


def save_token(token: str) -> None:
    """
    Save the Jamea JWT locally for reuse between runs.
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
    Load a previously saved Jamea JWT if it exists and has not expired.
    Deletes any expired token files encountered.
    """
    candidate_paths = [
        TOKEN_FILE,
        Path.home() / "Library" / "Application Support" / "Jadwal" / "data" / "jamea_token.json",
        Path.home() / "Library" / "Application Support" / "SWIFT" / "data" / "jamea_token.json",
    ]

    for path in candidate_paths:
        if not path.exists():
            continue
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)

            token = data.get("access_token")
            if isinstance(token, str) and token:
                if is_jwt_valid(token):
                    return token
                else:
                    print(f"Expired session token found in {path}. Removing...")
                    try:
                        path.unlink()
                    except OSError:
                        pass
        except (OSError, json.JSONDecodeError):
            pass

    return None


def delete_token() -> None:
    """
    Delete saved Jamea JWTs across all candidate locations.
    """
    candidate_paths = [
        TOKEN_FILE,
        Path.home() / "Library" / "Application Support" / "Jadwal" / "data" / "jamea_token.json",
        Path.home() / "Library" / "Application Support" / "SWIFT" / "data" / "jamea_token.json",
    ]
    for candidate in candidate_paths:
        if candidate.exists():
            try:
                candidate.unlink()
            except OSError:
                pass


async def get_token(page) -> str:
    """
    Read the Jamea JWT from browser sessionStorage or localStorage.
    """
    token = await page.evaluate("""
        () => {
            return sessionStorage.getItem("webauth_token_capture")
                || sessionStorage.getItem("WEBAUTH_TOKEN_CAPTURE")
                || localStorage.getItem("access_token")
                || sessionStorage.getItem("access_token");
        }
    """)

    if not token or not is_jwt_valid(token):
        raise RuntimeError(
            "Valid Jamea authentication token was not found. "
            "Please log in through the browser first."
        )

    return token


async def api_fetch(page, url: str, body: dict, token: str | None = None) -> Any:
    """
    Make an authenticated API request from inside the Jamea page context.
    Uses the provided token or reads it from page storage.
    """
    result = await page.evaluate(
        """
        async ({ url, body, token }) => {
            const authToken = token
                || sessionStorage.getItem("webauth_token_capture")
                || sessionStorage.getItem("WEBAUTH_TOKEN_CAPTURE")
                || localStorage.getItem("access_token")
                || sessionStorage.getItem("access_token");

            if (!authToken) {
                throw new Error("Authentication token not found.");
            }

            const response = await fetch(url, {
                method: "POST",
                headers: {
                    "Accept": "application/json, text/plain, */*",
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${authToken}`,
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
        {"url": url, "body": body, "token": token},
    )

    if not result["ok"]:
        if result["status"] == 401:
            raise RuntimeError("AUTH_EXPIRED")

        raise RuntimeError(
            f"API request failed: {result['status']} "
            f"{result['data']}"
        )

    return result["data"]


async def get_user_info_from_token(page, token: str | None = None) -> dict:
    """
    Decode JWT claims to discover the student's class, branch, and academic year.
    """
    if token and is_jwt_valid(token):
        return decode_jwt_claims(token)

    tok = await get_token(page)
    return decode_jwt_claims(tok)


async def get_current_week(page, year_ar: int, branch_id: str, token: str | None = None):
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
        },
        token=token
    )

    weeks = data.get("data", [])

    current_week = next(
        (week for week in weeks if week.get("isCurrent")),
        None
    )

    if current_week is None:
        raise RuntimeError("Could not find current Jamea week.")

    return current_week


async def generate_excel(page, report_params: dict, token: str | None = None) -> Path:
    url = (
        f"{API_BASE}/api/JadwalReport/"
        "JadwalTimeTableReportExcel"
    )

    response = await api_fetch(
        page,
        url,
        report_params,
        token=token
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
        async ({ url, token }) => {
            const authToken = token
                || sessionStorage.getItem("webauth_token_capture")
                || sessionStorage.getItem("WEBAUTH_TOKEN_CAPTURE")
                || localStorage.getItem("access_token")
                || sessionStorage.getItem("access_token");

            if (!authToken) {
                throw new Error("Authentication token not found.");
            }

            const response = await fetch(url, {
                method: "GET",
                headers: {
                    "Authorization": `Bearer ${authToken}`
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
        {"url": download_url, "token": token}
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
        active_token = None
        claims = None
        current_week = None
        authenticated = False

        context = await p.chromium.launch_persistent_context(
            user_data_dir=str(BROWSER_PROFILE),
            headless=False,
        )

        page = context.pages[0] if context.pages else await context.new_page()

        # Listen for any outgoing requests or responses carrying a valid Bearer token
        captured_tokens: list[str] = []

        def on_response(response):
            try:
                auth = response.request.headers.get("authorization", "")
                if auth.startswith("Bearer "):
                    cand = auth[7:].strip()
                    if is_jwt_valid(cand):
                        captured_tokens.append(cand)
            except Exception:
                pass

        page.on("response", on_response)

        if cached_token:
            print("Found saved Jamea authentication.")
            print("Testing saved session...")
            try:
                claims = decode_jwt_claims(cached_token)
                branch_id = str(claims["branchID"])
                year_ar = int(claims["yearAR"])

                await page.goto(JAMEA_URL, wait_until="domcontentloaded")

                current_week = await get_current_week(
                    page,
                    year_ar,
                    branch_id,
                    token=cached_token
                )
                active_token = cached_token
                authenticated = True
                print("Saved session is valid!")
            except Exception as e:
                if str(e) == "AUTH_EXPIRED":
                    print("Saved session has expired (AUTH_EXPIRED).")
                else:
                    print(f"Saved session expired or invalid: {e}")
                delete_token()
                cached_token = None
                authenticated = False

        if not authenticated:
            delete_token()
            try:
                await page.goto(JAMEA_URL, wait_until="domcontentloaded")
                # Clear any expired tokens from the page to allow clean ITS login
                await page.evaluate("""
                    () => {
                        sessionStorage.removeItem("webauth_token_capture");
                        sessionStorage.removeItem("WEBAUTH_TOKEN_CAPTURE");
                        sessionStorage.removeItem("access_token");
                        localStorage.removeItem("access_token");
                        localStorage.removeItem("refresh_token");
                    }
                """)
            except Exception:
                pass

            print()
            print("A browser window has been opened.")
            print("Please log in through ITS in the browser window.")
            print("Waiting for login to complete...")
            print()

            token = None
            for _ in range(180):  # Wait up to 180 seconds for user to log in
                await asyncio.sleep(1)
                try:
                    if page.is_closed():
                        raise RuntimeError("Browser window was closed before login completed.")

                    # Check for network captured tokens
                    while captured_tokens:
                        cand = captured_tokens.pop(0)
                        if is_jwt_valid(cand):
                            token = cand
                            break
                    if token:
                        break

                    # Check sessionStorage and localStorage
                    raw_token = await page.evaluate("""
                        () => {
                            return sessionStorage.getItem("webauth_token_capture")
                                || sessionStorage.getItem("WEBAUTH_TOKEN_CAPTURE")
                                || localStorage.getItem("access_token")
                                || sessionStorage.getItem("access_token");
                        }
                    """)
                    if raw_token and isinstance(raw_token, str) and is_jwt_valid(raw_token):
                        token = raw_token
                        break
                except Exception as e:
                    if "closed" in str(e).lower():
                        raise
                    continue

            if not token:
                raise RuntimeError("Login timed out. Please click Sync again and log in.")

            save_token(token)
            active_token = token
            print("Jamea authentication detected.")

            claims = decode_jwt_claims(active_token)
            branch_id = str(claims["branchID"])
            year_ar = int(claims["yearAR"])

            current_week = await get_current_week(
                page,
                year_ar,
                branch_id,
                token=active_token
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
            report_params,
            token=active_token
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